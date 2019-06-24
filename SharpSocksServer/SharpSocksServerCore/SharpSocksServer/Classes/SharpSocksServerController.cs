
using Common.Server.Interfaces;
using SocksServer.Classes.Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SharpSocksServer.Source.ImplantCommsHTTPServer.Interfaces;
using SharpSocksServer.Source.UI.Classes;

namespace SharpSocksServer.SharpSocksServer.Classes
{
    /// <summary>
    /// The job of the SharpSocksServerController is to create both the SOCKS Listener and the HTTP/S listener
    /// and to pass comms between the two
    /// </summary>
    public class SharpSocksServerController
    {
        public ILogOutput ServerComms { get { return _serverComms; } set { SocksProxy.ServerComms = _serverComms = value; } }
        public bool WaitOnConnect { get; set; }
		public uint SocketTimeout { get; set; }
		ILogOutput _serverComms;
        IServiceController _controller;
        Dictionary<ushort, TcpListener> _listeners = new Dictionary<ushort, TcpListener>();
		public List<ConnectionDetails> Status => SocksProxy.ConnectionDetails;

		public bool StartSocks(String ipToListen, ushort localPort, IServiceController controller, ManualResetEvent cmdChannelRunning = null)
        {
            _controller = controller;
            var onOff = (WaitOnConnect) ? "on" : "off";
            ServerComms.LogMessage($"Wait for Implant TCP Connect before SOCKS Proxy response is {onOff}");

            if (null != cmdChannelRunning)
            {
                Task.Factory.StartNew(() =>
                {
                    ServerComms.LogMessage($"Waiting for command channel before starting SOCKS proxy");
                    cmdChannelRunning.WaitOne();
                    StartSocksInternal(ipToListen, localPort);
                });
            }
            else
                return StartSocksInternal(ipToListen,localPort);

            return true;
        }

        bool StartSocksInternal(String ipToListen, ushort localPort)
        { 
            TcpListener tcs = null;
            try
            {
                var socksIp = ("*" == ipToListen) ? IPAddress.Any : IPAddress.Parse(ipToListen);
                tcs = new TcpListener(socksIp, localPort);
                _listeners.Add(localPort, tcs);
                tcs.Start();
                ServerComms.LogMessage($"Socks proxy listening started on {socksIp.ToString()}:{localPort}");
            }
            catch (Exception ex)
            {
                ServerComms.LogError($"StartSocks {ex.Message}");
                return false;
            }
            tcs.BeginAcceptTcpClient(AcceptTcpClient, tcs);
            return true;
        }

        void AcceptTcpClient(IAsyncResult iar)
        {
            var tcs = (TcpListener)iar.AsyncState;

            TcpClient tc = null;
            try
            {
                tc = tcs.EndAcceptTcpClient(iar);
            }
            catch(Exception ex)
            {
                if (_serverComms.IsVerboseOn())
                    _serverComms.LogError($"Initial SOCKS Read failed for endpoint {tcs.LocalEndpoint.ToString()} {ex.Message}".Trim());
                return;
            }
            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (ServerComms.IsVerboseOn())
                        ServerComms.LogMessage($"Message arrived {tcs.LocalEndpoint.ToString()} from {tc.Client.RemoteEndPoint.ToString()}".Trim());
                    (new SocksProxy() { TOTALSOCKETTIMEOUT = SocketTimeout }).ProcessRequest(tc, WaitOnConnect);
                }
                catch (Exception ex)
                {
                    ServerComms.LogError($"Error occured on EndPoint {tcs.LocalEndpoint.ToString()} shutting down cause of {ex.Message}".Trim());
                    if (tc.Connected)
                        tc.Close();
                    return;
                }
            });
            
            tcs.BeginAcceptTcpClient(AcceptTcpClient, tcs);
        }
        public void Stop()
        {
            SocksProxy.SocketComms.CloseAllConnections();
            _controller.Stop();
        }
    }
}
