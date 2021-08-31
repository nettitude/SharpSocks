using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SharpSocksServer.ImplantComms
{
    public class DataTask
    {
        private AutoResetEvent _wait = new(false);

        public AutoResetEvent Wait => _wait;

        public ConcurrentQueue<List<byte>> Tasks { get; } = new();

        public object PayloadLocker { get; } = new();

        public void DisposeWait()
        {
            Interlocked.Exchange(ref _wait, null).Dispose();
        }
    }
}