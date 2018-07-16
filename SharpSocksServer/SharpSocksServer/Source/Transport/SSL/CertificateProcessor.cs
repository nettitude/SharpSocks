using Common.Server.Interfaces;
using SharpSocksServer.Source.Helper.IP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpSocksServer.Source.Transport.SSL
{
    public class CertificateProcessor
    {
        readonly Regex _matchUriRgx = new Regex(@"(?<scheme>http[s]{0,1})[:]{1}//(?<domain>.*)[:]{0,1}(?<port>[0-9]{0,5})");
        static ILogOutput _serverComms { get; set; }
        static object _locker = new object();
        static IPTools _ipTools;
        static NetshWrapper _netshWrapper;
        public CertificateProcessor(ILogOutput scomms)
        {
            if(null == _serverComms)
            {
                lock (_locker)
                {
                    if (null == _serverComms)
                    {
                        _serverComms = scomms;
                        _ipTools = new IPTools() { ServerComms = _serverComms };
                        _netshWrapper = new NetshWrapper() { ServerComms = _serverComms };
                    }
                }
            }
        }

        public bool AddCertificateToHost(String strUri, X509Certificate2 pfx)
        {
            bool ssl = false;

            String host = "0.0.0.0"; //Drfault host
            String port = "443"; //Default port
            String domainHost = null;

            if (Uri.TryCreate(strUri, UriKind.RelativeOrAbsolute, out Uri uriToTest))
            {
                domainHost = uriToTest.Host;
                port = uriToTest.Port.ToString();
                if (uriToTest.Scheme == Uri.UriSchemeHttps)
                    ssl = true;
            }
            else
            {
                var hostingUrlParts = _matchUriRgx.Match(strUri);
                if (hostingUrlParts.Success)
                {
                    if (hostingUrlParts.Groups["scheme"].Value == "https")
                        ssl = true;
                    var rgxDomain = hostingUrlParts.Groups["domain"].Value;
                    domainHost = ((rgxDomain.StartsWith("*") && rgxDomain.EndsWith("*")) || (rgxDomain.StartsWith("*:"))) ? "0.0.0.0" : hostingUrlParts.Groups["domain"].Value;
                    if (rgxDomain.Contains(':'))
                    {
                        var splDom = rgxDomain.Split(':');
                        if (splDom.Length > 2)
                            _serverComms.LogError($"The URI {strUri} contains too many colons, please sort it out");
                        if (splDom[1].Contains('/'))
                            port = splDom[1].Split('/')[0];
                        else
                            port = splDom[1];
                       /* if (domainHost.Contains(':'))
                            domainHost = domainHost.[0];*/
                    }
                }
                port = (String.IsNullOrWhiteSpace(hostingUrlParts.Groups["port"].Value)) ? port : hostingUrlParts.Groups["port"].Value;
            }
            var hostIP = _ipTools.GetIPAddress(domainHost, out UriHostNameType type);
            if (UriHostNameType.IPv4 != type && UriHostNameType.IPv6 != type)
                _serverComms.LogError($"Can't resolve the host {strUri} to an IP so unable to bind the certificate");
            else
                host = hostIP.ToString();
            
            if (ssl)
            {
                var hdrs = new Dictionary<String, String>();
                if (_netshWrapper.CheckIfCertBoundToPort(host, port, ref hdrs))
                {
                    if (0 == hdrs.Count)
                    {
                        _serverComms.LogError($"No headers returned from netsh, that's not right");
                        return false;
                    }
                    _serverComms.LogMessage($"Certificate found on {hdrs["IP:port"]} with hash: {{{hdrs["Certificate Hash"]}}}");
                }
                else
                {
                    _serverComms.LogMessage($"No certificate found on {host}:{port}");
                    var certhash = pfx.GetCertHashString();
                    _serverComms.LogMessage($"Importing the default SharpSocks cert {{{certhash}}}");
                    if (!_netshWrapper.CheckIfCertIsInLMStore(pfx))
                    {
                        _serverComms.LogMessage($"Cert with hash {{{certhash}}} is not in the CA store adding now");
                        if (!_netshWrapper.ImportCertIntoLMStore(pfx))
                            throw new Exception($"Unable to import Certificate with hash {{{certhash}}} into CAStore)");
                    }
                    if (_netshWrapper.AddCertificateToIPPort(host, port, certhash))
                    {
                        _serverComms.LogMessage($"Cert {{{certhash}}}has been successfully added to {host}:{port}");
                        return true;
                    }
                    else
                    {
                        _serverComms.LogMessage($"ERROR: Cert {{{certhash}}} unable to be added to {host}:{port}");
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
