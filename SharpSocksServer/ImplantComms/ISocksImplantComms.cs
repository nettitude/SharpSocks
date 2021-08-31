using System.Collections.Generic;

namespace SharpSocksServer.ImplantComms
{
    public interface ISocksImplantComms
    {
        string CreateNewConnectionTarget(string target, ushort port);

        bool CloseTargetConnection(string targetId);

        void CloseAllConnections();

        bool SendDataToTarget(string target, List<byte> payload);
    }
}