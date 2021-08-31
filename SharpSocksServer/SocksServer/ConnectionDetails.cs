using SharpSocksCommon;

namespace SharpSocksServer.SocksServer
{
    public class ConnectionDetails
    {
        public ulong Id { get; set; }

        public string TargetId { get; set; }

        public string HostPort { get; set; }

        public CommandChannelStatus Status { get; set; }

        public int DataSent { get; set; }

        public int DataReceived { get; set; }

        public string UpdateTime { get; set; }
    }
}