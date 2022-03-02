namespace SharpSocksServer.SocksServer
{
    public class ConnectionDetails
    {
        public ulong Id { get; init; }

        public string HostPort { get; init; }

        public int DataSent { get; set; }

        public int DataReceived { get; set; }
    }
}