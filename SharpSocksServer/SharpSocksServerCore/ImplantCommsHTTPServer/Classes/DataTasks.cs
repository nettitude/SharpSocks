using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpSocksServer.Source.ImplantCommsHTTPServer.Classes
{
	public class DataTask
	{
		AutoResetEvent _wait = new AutoResetEvent(false);
		public AutoResetEvent Wait { get { return _wait; } } 
		public ConcurrentQueue<List<Byte>> Tasks { get; } = new ConcurrentQueue<List<byte>>();
		public object PayloadLocker { get; } = new object();
		public void DisposeWait() => Interlocked.Exchange(ref _wait, null).Dispose();
	}
}
