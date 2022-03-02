using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpSocksCommon;
using SharpSocksCommon.Utils;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;
using SharpSocksServer.SocksServer.Protocol;

namespace SharpSocksServer.SocksServer
{
    public class SocksProxy
    {
        private const int HEADER_LIMIT = ushort.MaxValue;
        private const int SOCKS_CONNECTION_TO_OPEN_TIMEOUT = 200000;
        private static ulong _internalCounter;
        private static readonly object INT_LOCKER = new();
        private static readonly ConcurrentDictionary<string, SocksProxy> MAP_TARGET_ID_TO_SOCKS_INSTANCE = new();
        private readonly AutoResetEvent _socksTimeout = new(false);
        private int _dataReceived;
        private int _dataSent;
        private bool _open;
        private bool _shutdownReceived;
        private CommandChannelStatus _status = CommandChannelStatus.CLOSED;
        private string _targetHost;
        private string _targetId;
        private ushort _targetPort;
        private TcpClient _tcpClient;
        private AutoResetEvent _timeoutEvent = new(false);
        private bool _waitOnConnect;

        public SocksProxy()
        {
            lock (INT_LOCKER)
            {
                Counter = ++_internalCounter;
            }
        }

        public uint TotalSocketTimeout { get; init; }

        private ulong Counter { get; }

        public static ILogOutput ServerComms { get; set; }

        public static ISocksImplantComms SocketComms { get; set; }

        public static ConnectionDetails GetDetailsForTargetId(string targetId)
        {
            if (!MAP_TARGET_ID_TO_SOCKS_INSTANCE.ContainsKey(targetId))
                return null;
            var socksProxy = MAP_TARGET_ID_TO_SOCKS_INSTANCE[targetId];
            return new ConnectionDetails
            {
                Id = socksProxy.Counter,
                HostPort = $"{socksProxy._targetHost}:{socksProxy._targetPort}",
                DataReceived = socksProxy._dataReceived,
                DataSent = socksProxy._dataSent
            };
        }

        public static void ReturnDataCallback(string target, List<byte> payload)
        {
            if (!MAP_TARGET_ID_TO_SOCKS_INSTANCE.ContainsKey(target))
            {
                ServerComms.LogError($"[{target}] Target {target} not found in Socks instance");
                return;
            }

            MAP_TARGET_ID_TO_SOCKS_INSTANCE[target].WriteResponseBackToClient(payload);
        }

        public static void NotifyConnection(string targetId, CommandChannelStatus status)
        {
            ServerComms.LogMessage($"[{targetId}][Implant -> SOCKS Server] Message has arrived back, status: {status}");
            if (!MAP_TARGET_ID_TO_SOCKS_INSTANCE.ContainsKey(targetId))
            {
                ServerComms.LogError($"[{targetId}][Implant -> SOCKS Server] Target {targetId} not found in Socks instance");
            }
            else
                switch (status)
                {
                    case CommandChannelStatus.CLOSED:
                        CloseConnection(targetId);
                        break;
                    case CommandChannelStatus.TIMEOUT:
                        MAP_TARGET_ID_TO_SOCKS_INSTANCE[targetId].ShutdownClient();
                        break;
                    case CommandChannelStatus.NO_CHANGE:
                    {
                        var socksProxy = MAP_TARGET_ID_TO_SOCKS_INSTANCE[targetId];
                        if (!socksProxy._waitOnConnect)
                            return;
                        socksProxy._socksTimeout.Set();
                        break;
                    }
                    case CommandChannelStatus.OPENING:
                    case CommandChannelStatus.OPEN:
                    case CommandChannelStatus.CONNECTED:
                    case CommandChannelStatus.CLOSING:
                    case CommandChannelStatus.FAILED:
                    case CommandChannelStatus.ASYNC_UPLOAD:
                    default:
                    {
                        var socksProxy = MAP_TARGET_ID_TO_SOCKS_INSTANCE[targetId];
                        socksProxy._open = status == CommandChannelStatus.OPEN;
                        socksProxy._status = status;
                        if (!socksProxy._waitOnConnect)
                            return;
                        socksProxy._socksTimeout.Set();
                        break;
                    }
                }
        }

        public static void CloseConnection(string targetId)
        {
            if (!MAP_TARGET_ID_TO_SOCKS_INSTANCE.ContainsKey(targetId))
                return;
            ServerComms.LogMessage($"[{targetId}][Client -> SOCKS Server] Close connection called, shutting client down...");
            MAP_TARGET_ID_TO_SOCKS_INSTANCE[targetId].ShutdownClient(true);
            if (!MAP_TARGET_ID_TO_SOCKS_INSTANCE.Remove(targetId, out _))
            {
                ServerComms.LogError($"[{targetId}][Client -> SOCKS Server] Unable to remove target from map");
            }
        }

        public static bool IsSessionOpen(string targetId)
        {
            return MAP_TARGET_ID_TO_SOCKS_INSTANCE.ContainsKey(targetId) && MAP_TARGET_ID_TO_SOCKS_INSTANCE[targetId]._status == CommandChannelStatus.OPEN;
        }

        public static bool IsValidSession(string targetId)
        {
            return MAP_TARGET_ID_TO_SOCKS_INSTANCE.ContainsKey(targetId);
        }

        private void ShutdownClient(bool implantNotified = false)
        {
            ServerComms.LogImportantMessage($"[{_targetId}] Shutdown called");
            _status = implantNotified ? CommandChannelStatus.CLOSING : CommandChannelStatus.TIMEOUT;
            _open = false;
            if (!_shutdownReceived)
            {
                _tcpClient.Close();
                _shutdownReceived = true;
                if (!string.IsNullOrWhiteSpace(_targetId))
                    if (!implantNotified)
                        SocketComms.CloseTargetConnection(_targetId);
            }

            if (_timeoutEvent == null)
                return;
            _timeoutEvent?.Close();
            _timeoutEvent = null;
        }

        public void ProcessRequest(TcpClient tcpClient, bool waitOnConnect = false)
        {
            _tcpClient = tcpClient;
            var stream = _tcpClient.GetStream();
            _waitOnConnect = waitOnConnect;
            if (!stream.CanRead)
            {
                if (tcpClient.Client.RemoteEndPoint is not IPEndPoint remoteEndPoint4)
                    return;
                ServerComms.LogError(
                    $"[{_targetId}][Client -> SOCKS Server] Failed reading SOCKS Connection from {IPAddress.Parse(remoteEndPoint4.Address.ToString())}:{remoteEndPoint4.Port.ToString()}");
                ShutdownClient();
            }
            else
            {
                try
                {
                    var source = new List<byte>();
                    var numArray = new byte[512000];
                    var count = stream.Read(numArray, 0, 512000);
                    source.AddRange(numArray.Take(count));
                    while (stream.CanRead && stream.DataAvailable)
                    {
                        count = stream.Read(numArray, 0, 512000);
                        source.AddRange(numArray.Take(count));
                    }

                    var responseCode = ProcessSocksHeaders(source.ToList());
                    var byteList = BuildSocks4Response(responseCode);
                    stream.Write(byteList.ToArray(), 0, byteList.Count);
                    stream.Flush();
                    if (Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_GRANTED == responseCode)
                        Task.Factory.StartNew((Action)(() => StartCommsWithProxyAndImplant(stream)));
                    else
                        ShutdownClient();
                }
                catch (Exception e)
                {
                    ShutdownClient();
                    if (tcpClient.Client.RemoteEndPoint is not IPEndPoint remoteEndPoint5)
                        return;
                    ServerComms.LogError(
                        $"[{_targetId}][Client -> SOCKS Server] Failed reading SOCKS Connection from {IPAddress.Parse(remoteEndPoint5.Address.ToString())}:{remoteEndPoint5.Port.ToString()}: {e}");
                }
            }
        }

        private void StartCommsWithProxyAndImplant(NetworkStream stream)
        {
            var buffer = new byte[1];
            try
            {
                var asyncBufferState = new AsyncBufferState
                {
                    Buffer = new byte[HEADER_LIMIT],
                    stream = stream,
                    ReceivedData = new AutoResetEvent(false)
                };
                var receivedData = asyncBufferState.ReceivedData;
                var num = 0;
                while (!_shutdownReceived)
                {
                    if (!_tcpClient.Connected || !TcpUtils.CheckTcpConnectionState(_tcpClient))
                    {
                        ServerComms.LogMessage($"[{_targetId}][Client -> SOCKS Server] Client no longer connected in start loop, shutting connection down...");
                        ShutdownClient();
                        return;
                    }

                    try
                    {
                        if (_shutdownReceived)
                            break;
                        _tcpClient.Client.Receive(buffer, SocketFlags.Peek);
                        if (stream.CanRead && stream.DataAvailable)
                        {
                            stream.BeginRead(asyncBufferState.Buffer, 0, HEADER_LIMIT, ProxySocketReadCallback, asyncBufferState);
                            receivedData.WaitOne((int)TotalSocketTimeout);
                            num = 0;
                        }
                        else
                        {
                            _timeoutEvent.WaitOne(50);
                        }
                    }
                    catch (Exception e)
                    {
                        ServerComms.LogError($"[{_targetId}][Client -> SOCKS Server] Connection to {_targetHost}:{_targetPort} has dropped: {e}");
                        ShutdownClient();
                        break;
                    }

                    if (num++ >= (int)TotalSocketTimeout / 100)
                    {
                        stream.Close();
                        ServerComms.LogError($"[{_targetId}][Client -> SOCKS Server] Connection closed to {_targetHost}:{_targetPort} after ({TotalSocketTimeout / 1000U}s) idle");
                        ShutdownClient();
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                ServerComms.LogError($"[{_targetId}][Client -> SOCKS Server] Connection to {_targetHost}:{_targetPort} has dropped: {e}");
                ShutdownClient();
            }
        }

        private void ProxySocketReadCallback(IAsyncResult asyncResult)
        {
            var asyncState = (AsyncBufferState)asyncResult.AsyncState;
            try
            {
                var stream = asyncState?.stream;
                if (stream == null) return;
                var count = stream.EndRead(asyncResult);
                if (_shutdownReceived || _tcpClient == null)
                    return;
                if (!_tcpClient.Connected || !TcpUtils.CheckTcpConnectionState(_tcpClient))
                {
                    ServerComms.LogMessage($"[{_targetId}][Client -> SOCKS Server] Client no longer connected on read attempt, shutting connection down...");
                    ShutdownClient();
                }
                else
                {
                    if (count <= 0)
                        return;
                    var payload = new List<byte>();
                    payload.AddRange(asyncState.Buffer.Take(count));
                    while (stream.CanRead && stream.DataAvailable)
                    {
                        if ((count = stream.Read(asyncState.Buffer, 0, HEADER_LIMIT)) > 0)
                            payload.AddRange(asyncState.Buffer.Take(count));
                    }

                    SocketComms.SendDataToTarget(_targetId, payload);
                    ServerComms.LogMessage($"[{_targetId}][Client -> SOCKS Server] Client sent {payload.Count} bytes ");
                    _dataSent += payload.Count;
                }
            }
            catch (Exception e)
            {
                try
                {
                    if (_tcpClient?.Client != null)
                        ServerComms.LogError($"[{_targetId}][Client -> SOCKS Server] Connection to {_tcpClient.Client.RemoteEndPoint} has dropped: {e}");
                }
                catch (ObjectDisposedException)
                {
                    ServerComms.LogError($"[{_targetId}][Client -> SOCKS Server] Connection to {_targetHost}:{_targetPort} has dropped: {e}");
                }

                ShutdownClient();
            }
            finally
            {
                asyncState?.ReceivedData.Set();
            }
        }

        private void WriteResponseBackToClient(List<byte> payload)
        {
            _dataReceived += payload.Count;
            if (_tcpClient.Connected)
                try
                {
                    var stream = _tcpClient.GetStream();
                    stream.Write(payload.ToArray(), 0, payload.Count);
                    stream.Flush();
                    ServerComms.LogMessage($"[{_targetId}][SOCKS Server -> Client] Wrote {payload.Count} bytes");
                    if (_tcpClient.Connected)
                        return;
                    ServerComms.LogMessage($"[{_targetId}][SOCKS Server -> Client] TCP Client no longer connection after writing, shutting down...");
                    ShutdownClient();
                }
                catch (Exception e)
                {
                    ServerComms.LogMessage($"[{_targetId}][SOCKS Server -> Client] Error writing data, shutting down client: {e}");
                    ShutdownClient();
                }
            else
            {
                ServerComms.LogMessage($"[{_targetId}][SOCKS Server -> Client] TCP Client no longer connection on attempt to write, shutting down...");
                ShutdownClient();
            }
        }

        private static List<byte> BuildSocks4Response(byte responseCode)
        {
            var byteList = new List<byte>
            {
                0,
                responseCode
            };
            var random = new Random((int)DateTime.Now.Ticks);
            byteList.AddRange(BitConverter.GetBytes(random.Next()).Take(2).ToArray());
            byteList.AddRange(BitConverter.GetBytes(random.Next()).ToArray());
            return byteList;
        }

        private byte ProcessSocksHeaders(IReadOnlyList<byte> buffer)
        {
            ServerComms.LogMessage($"[Client -> SOCKS Server] New SOCKS request");
            if (buffer.Count is < 9 or > 256)
            {
                ServerComms.LogError($"[Client -> SOCKS Server] Socks server: buffer size {buffer.Count} is not valid, must be between 9 & 256");
                return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
            }

            if (buffer[0] != 4)
                return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
            BitConverter.ToUInt16(buffer.Skip(2).Take(2).ToArray(), 0);
            BitConverter.ToUInt16(buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
            _targetPort = BitConverter.ToUInt16(buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
            var array1 = buffer.Skip(4).Take(4).ToArray();
            var source = buffer.Skip(8);
            var sourceArray = source as byte[] ?? source.ToArray();
            var count1 = sourceArray.ToList().IndexOf(0) + 1;
            if (-1 == count1)
            {
                ServerComms.LogError($"[Client -> SOCKS Server] User id is invalid rejecting connection request");
                return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
            }

            Encoding.UTF8.GetString(sourceArray.Take(count1).ToArray());
            if (array1[0] == 0 && array1[1] == 0 && array1[2] == 0 && array1[3] != 0)
            {
                var count2 = sourceArray.Skip(count1).ToList().IndexOf(0);
                var array2 = sourceArray.Skip(count1).Take(count2).ToArray();
                if (array2.Length == 0)
                {
                    ServerComms.LogError($"[Client -> SOCKS Server] Host name is empty rejecting connection request");
                    return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
                }

                var name = Encoding.UTF8.GetString(array2);
                if (Uri.CheckHostName(name) == UriHostNameType.Unknown)
                {
                    ServerComms.LogError($"[Client -> SOCKS Server] Host name {name} is invalid rejecting connection request");
                    return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
                }

                _targetHost = name;
            }
            else
            {
                _targetHost = new IPAddress(BitConverter.ToUInt32(array1, 0)).ToString();
            }

            ServerComms.LogMessage($"[Client -> SOCKS Server] SOCKS Request to open {_targetHost}:{_targetPort}");
            _status = CommandChannelStatus.OPENING;
            _targetId = SocketComms.CreateNewConnectionTarget(_targetHost, _targetPort);
            ServerComms.LogMessage($"[{_targetId}][Client -> SOCKS Server] GUID assigned to new connection to {_targetHost}:{_targetPort}");
            var socksProxy = this;
            MAP_TARGET_ID_TO_SOCKS_INSTANCE.TryAdd(_targetId, socksProxy);
            if (_waitOnConnect)
            {
                _socksTimeout.WaitOne(SOCKS_CONNECTION_TO_OPEN_TIMEOUT);
                if (!_open)
                    return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_REJECTED_OR_FAILED;
            }

            ServerComms.LogImportantMessage($"[{_targetId}][Client -> SOCKS Server] Opened SOCKS connection to {_targetHost}:{_targetPort}");
            return Socks4ClientHeader.Socks4ClientHeaderStatus.REQUEST_GRANTED;
        }

        private class AsyncBufferState
        {
            public NetworkStream stream;

            public byte[] Buffer { get; init; }

            public AutoResetEvent ReceivedData { get; init; }
        }
    }
}