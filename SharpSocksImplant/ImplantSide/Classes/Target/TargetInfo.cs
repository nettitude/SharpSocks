using System;
using System.Threading.Tasks;

namespace ImplantSide.Classes.Target
{
    public class TargetInfo
    {
        public String TargetHost { get; set; }
        public UInt16 TargetPort { get; set; }
        public System.Net.Sockets.TcpClient TargetTcpClient { set; get; }
        public bool Exit { get; set; }
        public Task ProxyLoop { get; set; }
    }
}
