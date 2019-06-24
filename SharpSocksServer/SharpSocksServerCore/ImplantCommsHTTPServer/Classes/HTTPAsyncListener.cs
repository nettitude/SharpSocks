using Common.Server.Interfaces;
using SocksTunnel.Interfaces;
using System;
using System.Collections.Generic;
using SharpSocksServer.Source.ImplantCommsHTTPServer.Interfaces;
using SharpSocksServer.Source.Transport.SSL;
using System.Security.Cryptography.X509Certificates;
using System.Collections;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace SocksTunnel.Classes
{
    public class HttpAsyncListener : IServiceController
    {
        System.Net.HttpListener _listener = null;
        IProcessRequest _iprocessRequest;
        public ILogOutput ServerComms { get; set; }
        
        public HttpAsyncListener(IProcessRequest processRequest, ILogOutput logOutput)
        {
            _iprocessRequest = processRequest;
            ServerComms = logOutput;
        }

        public void Stop()
        {
            _listener.Close();
        }
        
        public void CreateListener(Dictionary<String, X509Certificate2> prefixes)
        {
            var certProc = new CertificateProcessor(ServerComms);
            _listener = new System.Net.HttpListener();
            

            foreach (string s in prefixes.Keys)
            {
                certProc.AddCertificateToHost(s, prefixes[s]);
                _listener.Prefixes.Add(s);
            }
			_listener.Start();

			System.Threading.Tasks.Task.Factory.StartNew( () => {
				while (_listener.IsListening)
				{
					var result = _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
					result.AsyncWaitHandle.WaitOne();
				}
			});
			
		}

        X509Certificate2 GetDefaultSelfSignedCertFromResource()
        {
            var currasm = Assembly.GetExecutingAssembly();
            var pfxcmp = SharpSocksServer.SharpSocks.host2cert_pfx ;
            X509Certificate2 x5092 = null;
            
            using (var pfxcmpbts = new System.IO.MemoryStream(pfxcmp))
            using (var decompressedCert = new System.IO.MemoryStream())
            {
                using (var decompressionStream = new System.IO.Compression.DeflateStream(pfxcmpbts, System.IO.Compression.CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedCert);
                    decompressionStream.Close();
                    var sc = new System.Security.SecureString();
                    "SharpSocksKey".ToCharArray().ToList().ForEach(x => { sc.AppendChar(x); });
                    x5092 = new X509Certificate2(decompressedCert.ToArray(), sc);
                }
            }
            return x5092;
        }

        public void ListenerCallback(IAsyncResult result)
        {
            try
            {
                System.Net.HttpListener listener = (System.Net.HttpListener)result.AsyncState;
                System.Net.HttpListenerContext context = listener.EndGetContext(result);
                
                System.Net.HttpListenerRequest request = context.Request;
                System.Net.HttpListenerResponse response = context.Response;

                _iprocessRequest.ProcessRequest(context);
            }
            catch (Exception ex)
            {
                ServerComms.LogError($"Http Listener falled {ex.Message}");
                _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
            }
        }
    }
}
