using System;
using System.Threading.Tasks;

namespace ImplantSide.Classes.Target
{
    public class TargetInfo
    {
        public System.Net.IPAddress TargetIP { get; set; }
        public Int16 TargetPort { get; set; }
        public System.Net.Sockets.TcpClient TargetTcpClient { set; get; }
        public bool Exit { get; set; }
        public Task ProxyLoop { get; set; }
    }
}
