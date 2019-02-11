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
		public ManualResetEvent Wait { get; } = new ManualResetEvent(false);
		public Queue<List<Byte>> Tasks { get; } = new Queue<List<byte>>();
		public object PayloadLocker { get; } = new object();
	}
}
