using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;

namespace SharpSocksServer.SocksServer
{
    public class SharpSocksServerController
    {
        private readonly Dictionary<ushort, TcpListener> _listeners = new();
        private IServiceController _controller;
        private ILogOutput _serverComms;

        public ILogOutput ServerComms
        {
            get => _serverComms;
            set => SocksProxy.ServerComms = _serverComms = value;
        }

        public bool WaitOnConnect { get; set; }

        public uint SocketTimeout { get; set; }

        public List<ConnectionDetails> Status => SocksProxy.ConnectionDetails;

        public void StartSocks(string ipToListen, ushort localPort, IServiceController controller, ManualResetEvent cmdChannelRunning = null)
        {
            _controller = controller;
            ServerComms.LogMessage("Wait for Implant TCP Connect before SOCKS Proxy response is " + (WaitOnConnect ? "on" : "off"));
            if (cmdChannelRunning == null)
            {
                StartSocksInternal(ipToListen, localPort);
                return;
            }

            Task.Factory.StartNew((Action)(() =>
            {
                ServerComms.LogMessage("Waiting for command channel before starting SOCKS proxy");
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
                _listeners.Add(localPort, tcpListener);
                tcpListener.Start();
                ServerComms.LogMessage($"Socks proxy listening started on {localAddress}:{localPort}");
            }
            catch (Exception e)
            {
                ServerComms.LogError($"StartSocks error: {e}");
                return;
            }

            tcpListener.BeginAcceptTcpClient(AcceptTcpClient, tcpListener);
        }

        private void AcceptTcpClient(IAsyncResult asyncResult)
        {
            var tcpListener = (TcpListener)asyncResult.AsyncState;
            if (tcpListener == null)
            {
                _serverComms.LogError("[Client -> SOCKS Server] TCP Listener is null");
                return;
            }

            TcpClient tcpClient;
            try
            {
                tcpClient = tcpListener.EndAcceptTcpClient(asyncResult);
            }
            catch (Exception e)
            {
                _serverComms.LogError($"[Client -> SOCKS Server] Initial SOCKS Read failed for endpoint {tcpListener.LocalEndpoint}: {e}");
                return;
            }

            Task.Factory.StartNew((Action)(() =>
            {
                try
                {
                    ServerComms.LogMessage($"[Client -> SOCKS Server] New request from to {tcpListener.LocalEndpoint} from {tcpClient.Client.RemoteEndPoint}");
                    new SocksProxy
                    {
                        TotalSocketTimeout = SocketTimeout
                    }.ProcessRequest(tcpClient, WaitOnConnect);
                }
                catch (Exception e)
                {
                    ServerComms.LogError($"[Client -> SOCKS Server] Error occured on EndPoint {tcpListener.LocalEndpoint} shutting down cause of {e}");
                    if (!tcpClient.Connected)
                        return;
                    tcpClient.Close();
                }
            }));
            tcpListener.BeginAcceptTcpClient(AcceptTcpClient, tcpListener);
        }

        public void Stop()
        {
            SocksProxy.SocketComms.CloseAllConnections();
            _controller.Stop();
        }
    }
}