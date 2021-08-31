using System.Runtime.InteropServices;

namespace SharpSocksServer.SocksServer.Protocol
{
    [StructLayout(LayoutKind.Sequential)]
    public class Socks4ClientHeader
    {
        public int _arbitraryInt;
        public short _arbitraryShort;
        public byte _null;
        public Socks4ClientHeaderStatus status;

        public class Socks4ClientHeaderStatus
        {
            public static readonly byte REQUEST_GRANTED = 90;
            public static readonly byte REQUEST_REJECTED_OR_FAILED = 91;
            public static readonly byte REQUEST_FAILED_NO_IDENTED = 92;
            public static readonly byte REQUEST_FAILED_STRING_NOT_CONFIRMED = 93;
        }
    }
}