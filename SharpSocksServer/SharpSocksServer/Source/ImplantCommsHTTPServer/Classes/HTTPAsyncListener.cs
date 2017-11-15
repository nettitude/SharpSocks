using Common.Server.Interfaces;
using SocksTunnel.Interfaces;
using System;
using System.Collections.Generic;
using SharpSocksServer.Source.ImplantCommsHTTPServer.Interfaces;

namespace SocksTunnel.Classes
{
    public class HttpAsyncListener : IServiceController
    {
        System.Net.HttpListener _listener = null;
        IProcessRequest _iprocessRequest;
        public ILogOutput ServerComms { get; set; }
        
        public HttpAsyncListener(IProcessRequest processRequest)
        {
            _iprocessRequest = processRequest;
        }

        public void Stop()
        {
            _listener.Close();
        }
        
        public void CreateListener(List<string> prefixes)
        {
            _listener = new System.Net.HttpListener();
            foreach (string s in prefixes)
            {
                _listener.Prefixes.Add(s);
            }
            _listener.Start();

           var result = _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
        }

        public void ListenerCallback(IAsyncResult result)
        {
            try
            {
                System.Net.HttpListener listener = (System.Net.HttpListener)result.AsyncState;
                System.Net.HttpListenerContext context = listener.EndGetContext(result);
                _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);

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
