using System.Collections.Generic;

namespace SharpSocksServer.ImplantComms
{
    public interface ISocksImplantComms
    {
        string CreateNewConnectionTarget(string target, ushort port);

        void CloseTargetConnection(string targetId);

        void SendDataToTarget(string target, List<byte> payload);
    }
}