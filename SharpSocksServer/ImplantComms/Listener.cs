using System;
using SharpSocksServer.Constants;

namespace SharpSocksServer.ImplantComms
{
    public class Listener
    {
        public Listener(string target, ushort targetPort)
        {
            Target = target;
            TargetPort = targetPort;
            Status = ListenerStatus.Connecting;
        }

        public string Target { get; set; }

        public ushort TargetPort { get; set; }

        public ListenerStatus Status { get; set; }

        public Action ListenerConnectedCallback { get; set; }
    }
}