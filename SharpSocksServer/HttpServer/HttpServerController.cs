using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using SharpSocksServer.Config;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;

namespace SharpSocksServer.HttpServer
{
    public class HttpServerController
    {
        public HttpServerController(ILogOutput logger, EncryptedC2RequestProcessor requestProcessor, SharpSocksConfig config)
        {
            Logger = logger;
            RequestProcessor = requestProcessor;
            Config = config;
        }

        private ILogOutput Logger { get; }
        private EncryptedC2RequestProcessor RequestProcessor { get; }
        private SharpSocksConfig Config { get; }

        public void StartHttp()
        {
            var httpAsyncListener = new HttpAsyncListener(RequestProcessor, Logger);
            httpAsyncListener.CreateListener(new Dictionary<string, X509Certificate2>
            {
                {
                    Config.HttpServerURI,
                    null
                }
            });
            Logger.LogMessage($"C2 HTTP processor listening on {Config.HttpServerURI}");
        }
    }
}