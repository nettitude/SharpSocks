using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpSocksServer.Source.ImplantCommsHTTPServer.Classes
{
	public class DataTask
	{
		ManualResetEvent _wait = new ManualResetEvent(false);
		public ManualResetEvent Wait { get { return _wait; } } 
		public Queue<List<Byte>> Tasks { get; } = new Queue<List<byte>>();
		public object PayloadLocker { get; } = new object();
		public void DisposeWait() => Interlocked.Exchange(ref _wait, null).Dispose();
	}
}
