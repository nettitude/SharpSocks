using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocksServer.Classes.Socks
{
    public class Socks4aServerHeader : Socks4ServerHeader
    {
        /*
        field 1: SOCKS version number, 1 byte, must be 0x04 for this version
        field 2: command code, 1 byte:
            0x01 = establish a TCP/IP stream connection
            0x02 = establish a TCP/IP port binding
        field 3: port number, 2 bytes (in network byte order)
        field 4: IP address, 4 bytes (in network byte order)
        field 5: the user ID string, variable length, terminated with a null (0x00)
        field 6: the domain name of the host to contact, variable length, terminated with a null (0x00)
        */

        public String _domainName;
    }
}
