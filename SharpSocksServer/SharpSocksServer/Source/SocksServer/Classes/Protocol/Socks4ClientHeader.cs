using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SocksServer.Classes.Socks
{
    [StructLayout(LayoutKind.Sequential)]
    public class Socks4ClientHeader
    {
        /*
        field 1: null byte
        field 2: status, 1 byte:
            0x5A = request granted
            0x5B = request rejected or failed
            0x5C = request failed because client is not running identd (or not reachable from the server)
            0x5D = request failed because client's identd could not confirm the user ID string in the request
        field 3: 2 arbitrary bytes, which should be ignored
        field 4: 4 arbitrary bytes, which should be ignored
        */

        public class Socks4ClientHeaderStatus
        {
            public static readonly byte REQUEST_GRANTED = 0x5a;
            public static readonly byte REQUEST_REJECTED_OR_FAILED = 0x5b;
            public static readonly byte REQUEST_FAILED_NO_IDENTED = 0x5c;
            public static readonly byte REQUEST_FAILED_STRING_NOT_CONFIRMED = 0x5d;
        }

        public byte _null = 0x0;
        public Socks4ClientHeaderStatus status;
        public short _arbitraryShort;
        public Int32 _arbitraryInt;
    }
}
