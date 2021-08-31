using System.Net;

namespace SharpSocksServer.SocksServer.Protocol
{
    public class Socks4ServerHeader
    {
        public enum Socks4CommandCode : byte
        {
            TCPIP_STREAM_CONNECTION = 90, // 0x5A
            TCPIP_PORT_BINDING = 91 // 0x5B
        }

        public Socks4CommandCode _commandCode;
        public short _port;
        public IPAddress _targetIP;
        public string _userId;
        public byte _version;
    }
}