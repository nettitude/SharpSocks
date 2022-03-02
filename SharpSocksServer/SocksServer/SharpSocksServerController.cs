using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SharpSocksServer.Logging;

namespace SharpSocksServer.SocksServer
{
    public class SharpSocksServerController
    {
        private readonly ILogOutput _logger;

        public ILogOutput Logger
        {
            get => _logger;
            init => SocksProxy.ServerComms = _logger = value;
        }

        public bool WaitOnConnect { get; init; }

        public uint SocketTimeout { get; init; }

        public void StartSocks(string ipToListen, ushort localPort, ManualResetEvent cmdChannelRunning = null)
        {
            Logger.LogMessage($"Wait for Implant TCP Connect before SOCKS Proxy response is {(WaitOnConnect ? "on" : "off")}");
            if (cmdChannelRunning == null)
            {
                StartSocksInternal(ipToListen, localPort);
                return;
            }

            Task.Factory.StartNew((Action)(() =>
            {
                Logger.LogMessage("Waiting for command channel before starting SOCKS proxy");
                cmdChannelRunning.WaitOne();
                StartSocksInternal(ipToListen, localPort);
            }));
        }

        private void StartSocksInternal(string ipToListen, ushort localPort)
        {
            TcpListener tcpListener;
            try
            {
                var localAddress = "*" == ipToListen ? IPAddress.Any : IPAddress.Parse(ipToListen);
                tcpListener = new TcpListener(localAddress, localPort);
                tcpListener.Start();
                Logger.LogMessage($"Socks proxy listening started on {localAddress}:{localPort}");
            }
            catch (Exception e)
            {
                Logger.LogError($"StartSocks error: {e}");
                return;
            }

            tcpListener.BeginAcceptTcpClient(AcceptTcpClient, tcpListener);
        }

        private void AcceptTcpClient(IAsyncResult asyncResult)
        {
            var tcpListener = (TcpListener)asyncResult.AsyncState;
            if (tcpListener == null)
            {
                _logger.LogError("[Client -> SOCKS Server] TCP Listener is null");
                return;
            }

            TcpClient tcpClient;
            try
            {
                tcpClient = tcpListener.EndAcceptTcpClient(asyncResult);
            }
            catch (Exception e)
            {
                _logger.LogError($"[Client -> SOCKS Server] Initial SOCKS Read failed for endpoint {tcpListener.LocalEndpoint}: {e}");
                return;
            }

            Task.Factory.StartNew((Action)(() =>
            {
                try
                {
                    Logger.LogMessage($"[Client -> SOCKS Server] New request from to {tcpListener.LocalEndpoint} from {tcpClient.Client.RemoteEndPoint}");
                    new SocksProxy
                    {
                        TotalSocketTimeout = SocketTimeout
                    }.ProcessRequest(tcpClient, WaitOnConnect);
                }
                catch (Exception e)
                {
                    Logger.LogError($"[Client -> SOCKS Server] Error occured on EndPoint {tcpListener.LocalEndpoint} shutting down cause of {e}");
                    if (!tcpClient.Connected)
                        return;
                    tcpClient.Close();
                }
            }));
            tcpListener.BeginAcceptTcpClient(AcceptTcpClient, tcpListener);
        }
    }
}