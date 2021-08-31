using System;
using System.Linq;
using System.Net;
using SharpSocksServer.Logging;

namespace SharpSocksServer.Utils
{
    public class IPTools
    {
        public ILogOutput ServerComms { get; set; }

        public IPAddress GetIPAddress(string targetHost, out UriHostNameType typeOfAddr)
        {
            typeOfAddr = UriHostNameType.Basic;
            IPAddress ipAddress1 = null;
            try
            {
                var uriHostNameType = Uri.CheckHostName(targetHost);
                typeOfAddr = uriHostNameType;
                switch (uriHostNameType)
                {
                    case UriHostNameType.Dns:
                        var hostEntry = Dns.GetHostEntry(targetHost);
                        if (hostEntry != null)
                            if (hostEntry.AddressList != null)
                            {
                                var ipAddress2 = hostEntry.AddressList.First();
                                if (ipAddress2 != null)
                                    if (!string.IsNullOrWhiteSpace(ipAddress2.ToString()))
                                    {
                                        ipAddress1 = ipAddress2;
                                        typeOfAddr = Uri.CheckHostName(ipAddress1.ToString());
                                    }
                            }

                        break;
                    case UriHostNameType.IPv4:
                    case UriHostNameType.IPv6:
                        ipAddress1 = IPAddress.Parse(targetHost);
                        break;
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Failed to resolve typeof Host: " + ex.Message);
                return null;
            }

            return ipAddress1;
        }
    }
}