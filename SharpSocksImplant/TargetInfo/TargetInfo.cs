using System.Net.Sockets;
using System.Threading.Tasks;

namespace SharpSocksImplant.TargetInfo
{
    public class TargetInfo
    {
        public string TargetHost { get; set; }

        public ushort TargetPort { get; set; }

        public TcpClient TargetTcpClient { set; get; }

        public bool Exit { get; set; }

        public Task ProxyLoop { get; set; }
    }
}