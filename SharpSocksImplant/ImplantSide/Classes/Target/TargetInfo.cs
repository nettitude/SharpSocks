using SharpSocksImplant.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImplantSide.Classes.Target
{
    public class TargetInfo : IExitableTarget
	{
        public String TargetId { get; set; }
		public String TargetHost { get; set; }
        public UInt16 TargetPort { get; set; }
        public System.Net.Sockets.TcpClient TargetTcpClient { set; get; }
		int _exit = 0;
		public Action OnExit { get; set; }
        public bool Exit { get { return (_exit == 1); } 
			set {
				var val = (value) ? 1 : 0;
				var result = Interlocked.CompareExchange(ref _exit, val, 0);
				if ((result == 0) && value && null != OnExit)
					OnExit.Invoke();
			} 
		}
        public Task ProxyLoop { get; set; }
		public bool ConnectionAlive { get; set; }
		public ConcurrentQueue<List<byte>> ReadQueue { get; set; }
		public ConcurrentQueue<List<byte>> WriteQueue { get; set; }
		
		public TargetInfo()
		{
			ReadQueue = new ConcurrentQueue<List<byte>>();
			WriteQueue = new ConcurrentQueue<List<byte>>();
		}
	}
}
