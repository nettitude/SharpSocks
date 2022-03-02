using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;

namespace SharpSocksServer.SocksServer
{
    public class ServerController
    {
        public ILogOutput Logger { get; init; }

        public bool WaitOnConnect { get; init; }

        public uint SocketTimeout { get; init; }
        public EncryptedC2RequestProcessor RequestProcessor { get; set; }

        public void StartSocks(string ipToListen, ushort localPort)
        {
            Logger.LogMessage($"Wait for Implant TCP Connect before SOCKS Proxy response is {(WaitOnConnect ? "on" : "off")}");
            if (RequestProcessor.CmdChannelRunningEvent == null)
            {
                StartSocksInternal(ipToListen, localPort);
                return;
            }

            Task.Factory.StartNew((Action)(() =>
            {
                Logger.LogMessage("Waiting for command channel before starting SOCKS proxy");
                RequestProcessor.CmdChannelRunningEvent.WaitOne();
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
                Logger.LogError("[Client -> SOCKS Server] TCP Listener is null");
                return;
            }

            TcpClient tcpClient;
            try
            {
                tcpClient = tcpListener.EndAcceptTcpClient(asyncResult);
            }
            catch (Exception e)
            {
                Logger.LogError($"[Client -> SOCKS Server] Initial SOCKS Read failed for endpoint {tcpListener.LocalEndpoint}: {e}");
                return;
            }

            Task.Factory.StartNew((Action)(() =>
            {
                try
                {
                    Logger.LogMessage($"[Client -> SOCKS Server] New request from to {tcpListener.LocalEndpoint} from {tcpClient.Client.RemoteEndPoint}");
                    new SocksProxy
                    {
                        TotalSocketTimeout = SocketTimeout,
                        SocketComms = RequestProcessor
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