using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;

namespace SharpSocksServer.HttpServer
{
    public class HttpServerController
    {
        public HttpServerController(ILogOutput logger, EncryptedC2RequestProcessor requestProcessor)
        {
            Logger = logger;
            RequestProcessor = requestProcessor;
        }

        private ILogOutput Logger { get; }
        private EncryptedC2RequestProcessor RequestProcessor { get; }

        public void StartHttp(string httpServerUri)
        {
            var httpAsyncListener = new HttpAsyncListener(RequestProcessor, Logger);
            httpAsyncListener.CreateListener(new Dictionary<string, X509Certificate2>
            {
                {
                    httpServerUri,
                    null
                }
            });
            Logger.LogMessage($"C2 HTTP processor listening on {httpServerUri}");
        }
    }
}