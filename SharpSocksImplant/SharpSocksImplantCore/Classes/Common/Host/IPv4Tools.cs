using System;
using System.Linq;
using System.Text.RegularExpressions;


namespace SharpSocksImplantTestApp.Host
{
    public static class IPv4Tools
    {
        private static readonly Regex _ipRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
        //This is not designed to do a proper match on the port, it will only match numbers after the colon
        private static readonly Regex _simpleIpPortRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])(\/(?<port>(:\d[0-9].*?)))$");

        public static bool IsIP(string ip)
        {
            return _ipRegex.Match(ip).Success;
        }
        public static bool IsIPPort(string ipPort)
        {
            return _ipRegex.Match(ipPort).Success;
        }

        public static Match MatchIsIPPort(string ipPort)
        {
            return _ipRegex.Match(ipPort);
        }
    }
}
