using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSocksServer.ImplantCommsHTTPServer.Interfaces
{
    public interface IImplantSockComms
    {
        bool WritePayloadBackToSocksFromTarget(String targetid, List<byte> payload);
    }
}
