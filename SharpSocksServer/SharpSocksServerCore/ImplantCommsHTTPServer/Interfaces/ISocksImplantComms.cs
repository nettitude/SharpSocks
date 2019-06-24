using System;
using System.Collections.Generic;

namespace SharpSocksServer.ImplantCommsHTTPServer.Interfaces
{
    public interface ISocksImplantComms
    {
        String CreateNewConnectionTarget(String target, ushort port);
        bool CloseTargetConnection(String target);
        void CloseAllConnections();
        bool SendDataToTarget(String target, List<byte> payload);
    }
}
