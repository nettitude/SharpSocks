using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SharpSocksServer.Logging;

namespace SharpSocksServer.ImplantComms
{
    public class HttpAsyncListener
    {
        private readonly IProcessRequest _processRequest;
        private HttpListener _listener;

        public HttpAsyncListener(IProcessRequest processRequest, ILogOutput logOutput)
        {
            _processRequest = processRequest;
            ServerComms = logOutput;
        }

        private ILogOutput ServerComms { get; }

        public void CreateListener(Dictionary<string, X509Certificate2> prefixes)
        {
            _listener = new HttpListener();
            foreach (var key in prefixes.Keys)
            {
                _listener.Prefixes.Add(key);
            }

            _listener.Start();
            Task.Factory.StartNew((Action)(() =>
            {
                while (_listener.IsListening)
                    _listener.BeginGetContext(ListenerCallback, _listener).AsyncWaitHandle.WaitOne();
            }));
        }

        private void ListenerCallback(IAsyncResult result)
        {
            try
            {
                var context = ((HttpListener)result.AsyncState)?.EndGetContext(result);
                _processRequest.ProcessRequest(context);
            }
            catch (Exception e)
            {
                ServerComms.LogError($"HTTP Listener failed {e}");
                _listener.BeginGetContext(ListenerCallback, _listener);
            }
        }
    }
}