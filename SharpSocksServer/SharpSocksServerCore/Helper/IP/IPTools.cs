using Common.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSocksServer.Source.Helper.IP
{
    public class IPTools
    {
        public ILogOutput ServerComms { get; set; }
        public System.Net.IPAddress GetIPAddress(string targetHost, out UriHostNameType typeOfAddr)
        {
            typeOfAddr = UriHostNameType.Basic;
            System.Net.IPAddress targetIP = null;
            try
            {
                var hostNameType = Uri.CheckHostName(targetHost);
                typeOfAddr = hostNameType;
                switch (hostNameType)
                {
                    case UriHostNameType.Dns:
                        var iph = System.Net.Dns.GetHostEntry(targetHost);
                        if (null != iph && null != iph.AddressList)
                        {
                            var firstIP = iph.AddressList.First();
                            if (null != firstIP && !String.IsNullOrWhiteSpace(firstIP.ToString()))
                            {
                                targetIP = firstIP;
                                typeOfAddr =  Uri.CheckHostName(targetIP.ToString());
                            }
                        }
                        break;
                    case UriHostNameType.IPv6:
                    case UriHostNameType.IPv4:
                        targetIP = System.Net.IPAddress.Parse(targetHost);
                        break;
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Failed to resolve typeof Host: {ex.Message}");
                return null;
            }
            return targetIP;
        }
    }
}
