using System;
using System.Net;

namespace SocksServer.Classes.Socks
{
    public class Socks4ServerHeader
    {
        /*
        field 1: SOCKS version number, 1 byte, must be 0x04 for this version
        field 2: command code, 1 byte:
            0x01 = establish a TCP/IP stream connection
            0x02 = establish a TCP/IP port binding
        field 3: port number, 2 bytes (in network byte order)
        field 4: IP address, 4 bytes (in network byte order)
        field 5: the user ID string, variable length, terminated with a null (0x00)
        */
        public enum Socks4CommandCode : byte
        {
            TCPIP_STREAM_CONNECTION = 0x5a,
            TCPIP_PORT_BINDING = 0x5b
        }

        public byte _version;
        public Socks4CommandCode _commandCode;
        public short _port;
        public IPAddress _targetIP;
        public String _userId;
    }
}
