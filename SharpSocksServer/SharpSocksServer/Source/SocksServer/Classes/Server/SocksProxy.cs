using System;
using SocksServer.Classes.Socks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SharpSocksServer.ImplantCommsHTTPServer.Interfaces;
using Common.Server.Interfaces;
using System.Threading;
using SharpSocksServer.Source.UI.Classes;

namespace SocksServer.Classes.Server
{
    public class SocksProxy
    {
        static readonly Int32 HEADERLIMIT = 65535;
        static readonly Int32 BASEREADTIMEOUT = 500;
        //These control the timeout settings for reading back from socket
        static readonly int TOTALSOCKETTIMEOUT = 120000;
        static readonly ushort SOCKSCONNECTIONTOREADTIMEOUT = 10000;
        static readonly int SOCKSCONNECTIONTOOPENTIMEOUT = 200000;
        static readonly ushort TIMEBETWEENREADS = 200;
        
        public  static ILogOutput ServerComms { get; set;}
        public static ISocksImplantComms SocketComms { get; set; }
        AutoResetEvent TimeoutEvent = new AutoResetEvent(false);
        AutoResetEvent SocksTimeout = new AutoResetEvent(false);

        object _shutdownLocker = new object();
        int CurrentlyReading = 0;
        String _targetHost;
        ushort _targetPort;
        String status = "closed";
        Int32 _dataSent = 0;
        Int32 _dataRecv = 0;
        DateTime? LastUpdateTime = null;
        int timeout = BASEREADTIMEOUT;
        String _targetId = null;
        bool ShutdownRecieved = false;
        bool _waitOnConnect = false;
        bool _open = false;

        TcpClient _tc;
        static Dictionary<String, SocksProxy> mapTargetIdToSocksInstance = new Dictionary<string, SocksProxy>();
       
        public static List<ConnectionDetails> ConnectionDetails
        {
            get
            {
                return mapTargetIdToSocksInstance.Keys.ToList().Select(x => 
                    new ConnectionDetails() {
                        HostPort = $"{mapTargetIdToSocksInstance[x]._targetPort}:{mapTargetIdToSocksInstance[x]._targetPort}",
                        DataRecv = mapTargetIdToSocksInstance[x]._dataRecv,
                        DataSent = mapTargetIdToSocksInstance[x]._dataSent,
                        Id  = mapTargetIdToSocksInstance[x]._targetId,
                        Status = mapTargetIdToSocksInstance[x].status,
                        UpdateTime = (mapTargetIdToSocksInstance[x].LastUpdateTime.HasValue) ? mapTargetIdToSocksInstance[x].LastUpdateTime.Value.ToShortDateString() : "Never"
                    }
                ).ToList();
            }
        }

        public static bool ReturnDataCallback(String target, List<byte> payload)
        {
            if (ServerComms.IsVerboseOn())
                ServerComms.LogMessage($"Message has arrived back for {target}");

            if (!mapTargetIdToSocksInstance.ContainsKey(target))
            {
                ServerComms.LogError($"Target {target} not found in Socks instance");
                return false;
            }

            var socksInstance = mapTargetIdToSocksInstance[target];
            socksInstance.WriteResponseBackToClient(payload);
            return true;
        }

        public static void NotifyConnection(String target, String status)
        {
            if (ServerComms.IsVerboseOn())
                ServerComms.LogMessage($"Message has arrived back for {target}");

            if (!mapTargetIdToSocksInstance.ContainsKey(target))
            {
                ServerComms.LogError($"Target {target} not found in Socks instance");
                return;
            }
            var socksInstance = mapTargetIdToSocksInstance[target];

            if (status.ToLower() == "open")
                socksInstance._open = true;
            else
                socksInstance._open = false;

            socksInstance.status = status;
            socksInstance.LastUpdateTime = DateTime.Now;

            if (socksInstance._waitOnConnect)
                socksInstance.SocksTimeout.Set();
        }
        
        public static void ImplantCalledClose(String targetId)
        {
            var socksInstance = mapTargetIdToSocksInstance[targetId];
            socksInstance.ShutdownClient(true);
        }

        public static bool IsValidSession(String targetId)
        {
            return mapTargetIdToSocksInstance.ContainsKey(targetId);
        }

        void ShutdownClient(bool implantNotified = false)
        {
            status = (implantNotified) ? "closing (implant called close)" : "closing (SOCKS timeout)";
            LastUpdateTime = DateTime.Now;
            //Set the reading flag to stop any further threads coming in
            _open = false;
            Interlocked.CompareExchange(ref CurrentlyReading, 1, 0);
            if (!ShutdownRecieved)
            {
                lock (_shutdownLocker)
                {
                    if (!ShutdownRecieved)
                    {
                        ShutdownRecieved = true;
                        if (null != _tc && _tc.Connected)
                            _tc.Close();

                        if (String.IsNullOrWhiteSpace(_targetId) && !implantNotified)
                            SocketComms.CloseTargetConnection(_targetId);
                    }
                }

                if (mapTargetIdToSocksInstance.ContainsKey(_targetId))
                    mapTargetIdToSocksInstance.Remove(_targetId);

                TimeoutEvent.Close();
            }
        }

        public void ProcessRequest(TcpClient tc, bool waitOnConnect = false)
        {
            _tc = tc;
            var stream = _tc.GetStream();
            var timeout = 0;
            var timeoutCtr = 0;
            bool timedOut = false;
            _waitOnConnect = waitOnConnect;

            if (!stream.CanRead)
            {
                if (ServerComms.IsVerboseOn())
                    ServerComms.LogError($"Something attempted to connect but can't read from the stream");
            }

            while (!timedOut)
            {
                if (!stream.DataAvailable)
                {
                    if (1 == timeoutCtr)
                        timeout += TIMEBETWEENREADS;

                    TimeoutEvent.WaitOne(timeout);

                    if (timeoutCtr > (SOCKSCONNECTIONTOREADTIMEOUT / TIMEBETWEENREADS))
                    {
                        //Time out trying to read may as well shutdown the socket
                        ServerComms.LogError($"Timed out ({SOCKSCONNECTIONTOREADTIMEOUT / 1000}s) trying to read from SOCKS conection");
                        status = "closing (SOCKS time out)";
                        LastUpdateTime = DateTime.Now;
                        return;
                    }
                    timeoutCtr++;
                }
                else
                {
                    var bytesRead = 0;
                    var lstBuffer = new List<byte>();
                    var arrayBuffer = new byte[tc.Available];
                    
                    bytesRead = stream.Read(arrayBuffer, 0, tc.Available);
                    lstBuffer.AddRange(arrayBuffer);

                    while (bytesRead > 0 && stream.DataAvailable)
                    {
                        arrayBuffer = new byte[tc.Available];
                        bytesRead += stream.Read(arrayBuffer, 0, tc.Available);
                        lstBuffer.AddRange(arrayBuffer);
                    }
                    
                    var procResult = ProcessSocksHeaders(lstBuffer.ToList());
                    var responsePacket = BuildSocks4Response(procResult);
                    
                    stream.Write(responsePacket.ToArray(), 0, responsePacket.Count);
                    stream.Flush();

                    if (Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_GRANTED == procResult)
                        StartCommsWithProxyAndImplant(stream);
                    else    
                        _tc.Close();

                    return;
                }
            }
        }
        public class AsyncBufferState
        {
            public byte[] Buffer = new Byte[HEADERLIMIT];
            public NetworkStream Stream;
        }

        void StartCommsWithProxyAndImplant(NetworkStream stream)
        {
            //Check the currently flag as we multiple threads can hit this
            //Interlocked uses an atomic. If there is a thread already reading just bang out here
            if ((Interlocked.CompareExchange(ref CurrentlyReading, 1, 0) == 1))
                return;

            var timeout = 0;
            var timeoutCtr = 0;
            //Shutdown has been recieved on another thread we outta here
            while (!ShutdownRecieved)
            {
                if (!stream.DataAvailable)
                {
                    if (1 == timeoutCtr)
                        timeout += TIMEBETWEENREADS;

                    TimeoutEvent.WaitOne(timeout);
                }
                else
                { 
                    if (!stream.CanRead || !_tc.Connected)
                    {
                        ShutdownClient();
                        ServerComms.LogError($"Connection to {_targetHost}:{_targetPort} has dropped before data sent");
                        return;
                    }
                    try
                    {
                        //Quick check here just in case....
                        if (ShutdownRecieved)
                            return;
                        var asyncBufferState = new AsyncBufferState() { Stream = stream };
                        stream.BeginRead(asyncBufferState.Buffer, 0, HEADERLIMIT, ProxySocketReadCallback, asyncBufferState);
                        return;
                    }
                    catch (Exception ex)
                    {
                        ServerComms.LogError($"Connection to {_targetHost}:{_targetPort} has dropped: {ex.Message}");
                        ShutdownClient();
                    }
                }

                if (timeoutCtr > (TOTALSOCKETTIMEOUT / TIMEBETWEENREADS))
                {
                    //Time out trying to read may as well shutdown the socket
                    ServerComms.LogError($"Connection closed to {_targetHost}:{_targetPort} after ({TOTALSOCKETTIMEOUT / 1000}s) idle");
                    ShutdownClient();
                    return;
                }
                timeoutCtr++;
            }
        }

        void ProxySocketReadCallback(IAsyncResult iar)
        {
            var asyncState = (AsyncBufferState)iar.AsyncState;
            var stream = asyncState.Stream;
            int bytesRead = 0;
            if (ShutdownRecieved || null == _tc)
                return;

            if (!_tc.Connected)
            {
                ShutdownClient();
                try
                {
                    if (_tc.Client != null)
                    {
                        ServerComms.LogError($"Connection to {_tc.Client.RemoteEndPoint.ToString()} closed");
                    }
                }
                catch (ObjectDisposedException)
                {
                    ServerComms.LogError($"Connection to {_targetHost}:{_targetPort} closed");
                }
                return;
            }   
            try
            {
                bytesRead = stream.EndRead(iar);
                _dataSent += bytesRead;

                if (bytesRead > 0)
                { 
                    var payload = new List<byte>();
                    payload.AddRange(asyncState.Buffer.Take(bytesRead));
                    Array.Clear(asyncState.Buffer, 0, asyncState.Buffer.Length);
                    while (stream.CanRead && stream.DataAvailable && (bytesRead = stream.Read(asyncState.Buffer, 0, HEADERLIMIT)) > 0)
                    {
                        payload.AddRange(asyncState.Buffer.Take(bytesRead));
                        Array.Clear(asyncState.Buffer, 0, asyncState.Buffer.Length);
                    }
                    //Not currently reading now so if anything comes in fair play
                    Interlocked.Decrement(ref CurrentlyReading);
                    ServerComms.LogMessage($"Client sent data (size: {payload.Count}) {_targetId} writing to Implant");
                    SocketComms.SendDataToTarget(_targetId, payload);
                    StartCommsWithProxyAndImplant(asyncState.Stream);
                }
                else
                {
                    //No bytes have been read from the connection 
                    //Try again and start the thread timeout cycle
                    TimeoutEvent.WaitOne(timeout);
                    StartCommsWithProxyAndImplant(asyncState.Stream);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (_tc.Client != null)
                    {
                        ServerComms.LogError($"Connection to {_tc.Client.RemoteEndPoint.ToString()} has dropped cause {ex.Message}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    ServerComms.LogError($"Connection to {_targetHost}:{_targetPort} has dropped cause {ex.Message}");
                }
            }
        }

        void WriteResponseBackToClient(List<byte> payload)
        {
            _dataRecv += payload.Count();
            ServerComms.LogMessage($"Recieved payload back from Implant (size: {payload.Count} for {_targetId} writing to client");
            if (_tc.Connected)
            {
                var stream = _tc.GetStream();
                stream.Write(payload.ToArray(), 0, payload.Count);
                stream.Flush();
                StartCommsWithProxyAndImplant(stream);
            }
            else
            {
                ShutdownClient();
            }
        }

        List<byte> BuildSocks4Response(byte responeCode)
        {
            var resp = new List<byte>() {0x0, responeCode };
            var ran = new Random((Int32)DateTime.Now.Ticks);
            resp.AddRange(BitConverter.GetBytes(ran.Next()).Take(2).ToArray());
            resp.AddRange(BitConverter.GetBytes(ran.Next()).ToArray());

            return resp;
        }

        byte ProcessSocksHeaders(List<byte> buffer)
        {
            if (9 > buffer.Count || 265 < buffer.Count)
            {
                ServerComms.LogError($"Socks server: buffer size {buffer.Count} is not valid, must be between 9 & 256");
                return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
            }
            byte version = buffer[0];
            if (version == 0x4)
            {
                byte commandCode = buffer.Skip(1).Take(1).First();
                BitConverter.ToUInt16(buffer.Skip(2).Take(2).ToArray(), 0);
                BitConverter.ToUInt16(buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
                _targetPort = BitConverter.ToUInt16(buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
                var dstIp = buffer.Skip(4).Take(4).ToArray();

                var tailBuffer = buffer.Skip(8);
                var endUserIdx = tailBuffer.ToList().IndexOf(0x0) + 1;
                if (-1 == endUserIdx)
                {
                    ServerComms.LogError($"User id is invalid rejecting connection request");
                    return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
                }
                var userId = UTF8Encoding.UTF8.GetString(tailBuffer.Take(endUserIdx).ToArray());

                //Check if SOCKS 4a and domain name specified 
                //If the domain name is to follow the IP will be in the format 0.0.0.x
                if (0 == dstIp[0] && 0 == dstIp[1] && 0 == dstIp[2] && 0 != dstIp[3])
                {

                    var endHostIdx = tailBuffer.Skip(endUserIdx).ToList().IndexOf(0x0);
                    var arrayHost = tailBuffer.Skip(endUserIdx).Take(endHostIdx).ToArray();
                    if (arrayHost.Length == 0)
                    {
                        ServerComms.LogError($"Host name is empty rejecting connection request");
                        return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
                    }
                    var dnsHost = UTF8Encoding.UTF8.GetString(arrayHost);
                    if(UriHostNameType.Unknown == Uri.CheckHostName(dnsHost))
                    {
                        ServerComms.LogError($"Host name {dnsHost} is invalid rejecting connection request");
                        return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
                    }
                    _targetHost = dnsHost;
                }
                else
                    _targetHost = new IPAddress(BitConverter.ToUInt32(dstIp, 0)).ToString();

               ServerComms.LogMessage($"Recieved SOCKS message to open port {_targetPort} on {_targetHost}");
                status = "opening";
                LastUpdateTime = DateTime.Now;

                _targetId = SocketComms.CreateNewConnectionTarget(_targetHost, _targetPort);
                var thisptr = this;
                if (null == thisptr)
                    ServerComms.LogError("This pointer is NULL something wrong here");

                mapTargetIdToSocksInstance.Add(_targetId, thisptr);
                
                if(_waitOnConnect)
                {
                    SocksTimeout.WaitOne(SOCKSCONNECTIONTOOPENTIMEOUT);
                    if (!_open)
                        return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
                }
            }
            else
                return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;

            ServerComms.LogMessage($"Opened SOCKS port {_targetPort} on {_targetHost}, targetid {_targetId}");
            return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_GRANTED;
        }
    }
}
