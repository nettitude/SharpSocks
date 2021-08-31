using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SharpSocksServer.Logging;
using SharpSocksServer.Transport.SSL;

namespace SharpSocksServer.ImplantComms
{
    public class HttpAsyncListener : IServiceController
    {
        private readonly IProcessRequest _iprocessRequest;
        private HttpListener _listener;

        public HttpAsyncListener(IProcessRequest processRequest, ILogOutput logOutput)
        {
            _iprocessRequest = processRequest;
            ServerComms = logOutput;
        }

        public ILogOutput ServerComms { get; set; }

        public void Stop()
        {
            _listener.Close();
        }

        public void CreateListener(Dictionary<string, X509Certificate2> prefixes)
        {
            var certificateProcessor = new CertificateProcessor(ServerComms);
            _listener = new HttpListener();
            foreach (var key in prefixes.Keys)
            {
                certificateProcessor.AddCertificateToHost(key, prefixes[key]);
                _listener.Prefixes.Add(key);
            }

            _listener.Start();
            Task.Factory.StartNew((Action)(() =>
            {
                while (_listener.IsListening)
                    _listener.BeginGetContext(ListenerCallback, _listener).AsyncWaitHandle.WaitOne();
            }));
        }

        private X509Certificate2 GetDefaultSelfSignedCertFromResource()
        {
            Assembly.GetExecutingAssembly();
            using var memoryStream1 = new MemoryStream(SharpSocks.host2cert_pfx);
            using var memoryStream2 = new MemoryStream();
            using var deflateStream = new DeflateStream(memoryStream1, CompressionMode.Decompress);
            deflateStream.CopyTo(memoryStream2);
            deflateStream.Close();
            var sc = new SecureString();
            "SharpSocksKey".ToCharArray().ToList().ForEach((Action<char>)(x => sc.AppendChar(x)));
            return new X509Certificate2(memoryStream2.ToArray(), sc);
        }

        public void ListenerCallback(IAsyncResult result)
        {
            try
            {
                var context = ((HttpListener)result.AsyncState).EndGetContext(result);
                _iprocessRequest.ProcessRequest(context);
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Http Listener falled " + ex.Message);
                _listener.BeginGetContext(ListenerCallback, _listener);
            }
        }
    }
}