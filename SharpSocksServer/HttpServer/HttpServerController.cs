using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

            if (Config.HttpServerURI.ToLower().StartsWith("https://"))
            {
                var cert = GetDefaultSelfSignedCertFromResource();
                builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(adapterOptions => adapterOptions.ServerCertificate = cert));
            }

            var app = builder.Build();

            app.MapGet("{**url}", _requestHandler.HandleRequest);
            app.MapPost("{**url}", _requestHandler.HandleRequest);
            app.Run(Config.HttpServerURI);
        }

        private X509Certificate2 GetDefaultSelfSignedCertFromResource()
        {
            Assembly.GetExecutingAssembly();
            using var certStream = new MemoryStream(SharpSocks.SharpSocksCert);
            var secureString = new SecureString();
            Config.CertificateKey.ToCharArray().ToList().ForEach((Action<char>)(x => secureString.AppendChar(x)));
            return new X509Certificate2(certStream.ToArray(), secureString);
        }
    }
}