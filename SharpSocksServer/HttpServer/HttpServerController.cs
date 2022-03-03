using Microsoft.AspNetCore.Builder;
using SharpSocksServer.Config;
using SharpSocksServer.Logging;

namespace SharpSocksServer.HttpServer
{
    public class HttpServerController
    {
        private readonly HttpRequestHandler _requestHandler;

        public HttpServerController(ILogOutput logger, SharpSocksConfig config, HttpRequestHandler requestHandler)
        {
            _requestHandler = requestHandler;
            Logger = logger;
            Config = config;
        }

        private ILogOutput Logger { get; }
        private SharpSocksConfig Config { get; }

        public void StartHttp()
        {
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("{uri:regex(/*)}", _requestHandler.HandleRequest);
            app.MapPost("{uri:regex(/*)}", _requestHandler.HandleRequest);

            app.Run(Config.HttpServerURI);
            Logger.LogMessage($"C2 HTTP processor listening on {Config.HttpServerURI}");
        }
    }
}