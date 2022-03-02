using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;

namespace SharpSocksServer.HttpServer
{
    public class HttpServerController
    {
        public ILogOutput Logger { get; init; }
        public EncryptedC2RequestProcessor RequestProcessor { get; init; }

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