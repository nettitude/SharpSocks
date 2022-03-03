using System.Collections.Generic;
using System.Threading;

namespace SharpSocksServer.SocksServer
{
    public interface ISocksImplantComms
    {
        string CreateNewConnectionTarget(string target, ushort port);

        void CloseTargetConnection(string targetId);

        void SendDataToTarget(string target, List<byte> payload);

        ManualResetEvent GetCommandChannelRunningEvent();
    }
}