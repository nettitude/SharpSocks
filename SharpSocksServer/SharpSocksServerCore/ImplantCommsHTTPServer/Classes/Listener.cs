using SocksTunnel.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocksTunnel
{
    public class Listener
    {
        public Listener(String target, ushort targetPort)
        {
            Target = target;
            TargetPort = targetPort;
            Status = ListenerStatus.Connecting;
        }

        public String Target { get; set; }
        public ushort TargetPort { get; set; }
        public ListenerStatus Status { get; set; }
        public Action ListenerConnectedCallback { get; set; }
    }
}
