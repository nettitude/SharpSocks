using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using SharpSocksServer.Logging;
using SharpSocksServer.Utils;

namespace SharpSocksServer.Transport.SSL
{
    public class CertificateProcessor
    {
        private static readonly object _locker = new();
        private static IPTools _ipTools;
        private static NetshWrapper _netshWrapper;
        private readonly Regex _matchUriRgx = new("(?<scheme>http[s]{0,1})[:]{1}//(?<domain>.*)[:]{0,1}(?<port>[0-9]{0,5})");
        private X509Certificate2 defaultCert;

        public CertificateProcessor(ILogOutput scomms)
        {
            if (_serverComms != null)
                return;
            lock (_locker)
            {
                if (_serverComms != null)
                    return;
                _serverComms = scomms;
                _ipTools = new IPTools
                {
                    ServerComms = _serverComms
                };
                _netshWrapper = new NetshWrapper
                {
                    ServerComms = _serverComms
                };
            }
        }

        private static ILogOutput _serverComms { get; set; }

        private X509Certificate2 GetDefaultSelfSignedCertFromResource()
        {
            Assembly.GetExecutingAssembly();
            using var memoryStream1 = new MemoryStream(SharpSocks.host2cert_pfx);
            using var memoryStream2 = new MemoryStream();
            using var deflateStream = new DeflateStream(memoryStream1, CompressionMode.Decompress);
            deflateStream.CopyTo(memoryStream2);
            deflateStream.Close();
            var sc = new SecureString();
            "SharpSocksKey".ToCharArray().ToList().ForEach((Action<char>)(x => sc.AppendChar(x)));
            return new X509Certificate2(memoryStream2.ToArray(), sc);
        }

        public bool AddCertificateToHost(string strUri, X509Certificate2 pfx)
        {
            var flag = false;
            var host = "0.0.0.0";
            var str1 = "443";
            string targetHost = null;
            string port;
            if (Uri.TryCreate(strUri, UriKind.RelativeOrAbsolute, out var result))
            {
                targetHost = result.Host;
                port = result.Port.ToString();
                if (result.Scheme == Uri.UriSchemeHttps)
                    flag = true;
            }
            else
            {
                var match = _matchUriRgx.Match(strUri);
                if (match.Success)
                {
                    if (match.Groups["scheme"].Value == "https")
                        flag = true;
                    var str2 = match.Groups["domain"].Value;
                    targetHost = str2.StartsWith("*") && str2.EndsWith("*") || str2.StartsWith("*:") ? "0.0.0.0" : match.Groups["domain"].Value;
                    if (str2.Contains(':'))
                    {
                        var strArray = str2.Split(':');
                        if (strArray.Length > 2)
                            _serverComms.LogError("The URI " + strUri + " contains too many colons, please sort it out");
                        str1 = !strArray[1].Contains('/') ? strArray[1] : strArray[1].Split('/')[0];
                    }
                }

                port = string.IsNullOrWhiteSpace(match.Groups["port"].Value) ? str1 : match.Groups["port"].Value;
            }

            var ipAddress = _ipTools.GetIPAddress(targetHost, out var typeOfAddr);
            if (UriHostNameType.IPv4 != typeOfAddr && UriHostNameType.IPv6 != typeOfAddr)
                _serverComms.LogError("Can't resolve the host " + strUri + " to an IP so unable to bind the certificate");
            else
                host = ipAddress.ToString();
            if (!flag)
                return false;
            if (pfx == null && defaultCert == null)
            {
                pfx = defaultCert = GetDefaultSelfSignedCertFromResource();
                _serverComms.LogMessage("No cert specified for " + host + " unless already bound will use the default");
            }

            var certDetails = new Dictionary<string, string>();
            if (_netshWrapper.CheckIfCertBoundToPort(host, port, ref certDetails))
            {
                if (certDetails.Count == 0)
                {
                    _serverComms.LogError("No headers returned from netsh, that's not right");
                    return false;
                }

                _serverComms.LogMessage("Certificate found on " + certDetails["IP:port"] + " with hash: {" + certDetails["Certificate Hash"] + "}");
                return true;
            }

            _serverComms.LogMessage("No certificate found on " + host + ":" + port);
            var certHashString = pfx.GetCertHashString();
            _serverComms.LogMessage("Importing the default SharpSocks cert {" + certHashString + "}");
            if (!_netshWrapper.CheckIfCertIsInLMStore(pfx))
            {
                _serverComms.LogMessage("Cert with hash {" + certHashString + "} is not in the CA store adding now");
                if (!_netshWrapper.ImportCertIntoLMStore(pfx))
                    throw new Exception("Unable to import Certificate with hash {" + certHashString + "} into CAStore)");
            }

            if (_netshWrapper.AddCertificateToIPPort(host, port, certHashString))
            {
                _serverComms.LogMessage("Cert {" + certHashString + "}has been successfully added to " + host + ":" + port);
                return true;
            }

            _serverComms.LogMessage("ERROR: Cert {" + certHashString + "} unable to be added to " + host + ":" + port);
            return false;
        }
    }
}