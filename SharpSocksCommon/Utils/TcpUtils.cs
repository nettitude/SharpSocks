using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SharpSocksCommon.Utils
{
    public static class TcpUtils
    {
        public static bool CheckTcpConnectionState(TcpClient tcpClient)
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = ipProperties.GetActiveTcpConnections()
                .Where(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint)).ToArray();

            if (tcpConnections.Length > 0)
            {
                var stateOfConnection = tcpConnections.First().State;
                if (stateOfConnection == TcpState.Established)
                {
                    return true;
                }
            }

            return false;
        }
    }
}