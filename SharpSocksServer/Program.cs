using System;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using SharpSocksCommon.Encryption;
using SharpSocksServer.Config;
using SharpSocksServer.HttpServer;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;
using SharpSocksServer.SocksServer;

namespace SharpSocksServer
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("SharpSocks Server\r\n=================\n");
            try
            {
                var config = ParseArgs(args);
                if (config == null)
                {
                    return;
                }
                Console.WriteLine("[*] Initialising...");

                var services = new ServiceCollection()
                    .AddSingleton(config)
                    .AddSingleton<IEncryptionHelper>(x =>
                        new RijndaelCBCCryptor(x.GetRequiredService<SharpSocksConfig>().EncryptionKey))
                    .AddSingleton<EncryptedC2RequestProcessor>()
                    .AddSingleton<IProcessRequest>(x => x.GetRequiredService<EncryptedC2RequestProcessor>())
                    .AddSingleton<ISocksImplantComms>(x => x.GetRequiredService<EncryptedC2RequestProcessor>())
                    .AddSingleton<HttpServerController>()
                    .AddSingleton<SocksServerController>()
                    .AddSingleton<HttpRequestHandler>()
                    .AddSingleton<ILogOutput, ConsoleOutput>();

                var serviceProvider = services.BuildServiceProvider();

                var logger = serviceProvider.GetRequiredService<ILogOutput>();
                SocksProxy.Logger = logger;

                var httpServerController = serviceProvider.GetRequiredService<HttpServerController>();
                var serverController = serviceProvider.GetRequiredService<SocksServerController>();
                Console.WriteLine("[*] Initialised");
                serverController.StartSocks();
                httpServerController.StartHttp();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Fatal Error: {e}");
            }
        }

        private static SharpSocksConfig ParseArgs(string[] args)
        {
            var app = new CommandLineApplication();
            app.HelpOption();
            var optSocksServerUri = app.Option("-s|--socksserveruri", "IP:Port for SOCKS to listen on, default is *:43334", CommandOptionType.SingleValue);
            var optCmdChannelId = app.Option("-c|--cmdid", "Command Channel Identifier, needs to be shared with the server", CommandOptionType.SingleValue);
            var optHttpServer = app.Option("-l|--httpserveruri", "Uri to listen on, default is http://127.0.0.1:8081", CommandOptionType.SingleValue);
            var optEncKey = app.Option("-k|--encryptionkey", "The encryption key used to secure comms", CommandOptionType.SingleValue);
            var optSessionCookie = app.Option("-sc|--sessioncookie", "The name of the cookie to pass the session identifier", CommandOptionType.SingleValue);
            var optPayloadCookie = app.Option("-pc|--payloadcookie", "The name of the cookie to pass smaller requests through", CommandOptionType.SingleValue);
            var optSocketTimeout = app.Option("-st|--socketTimeout", "How long should SOCKS sockets be held open for, default is 30s", CommandOptionType.SingleValue);
            var optVerbose = app.Option("-v|--verbose", "Verbose error logging", CommandOptionType.NoValue);
            var pfxPassword = app.Option("-p|--pfxpassword", "Password to the PFX certificate if using HTTPS", CommandOptionType.SingleValue);

            SharpSocksConfig config = null;
            app.OnExecute(() =>
            {
                config = SharpSocksConfig.LoadConfig(optSocksServerUri, optSocketTimeout, optCmdChannelId, optEncKey, optSessionCookie, optPayloadCookie,
                    optVerbose, optHttpServer, pfxPassword);
            });
            app.Execute(args);
            return config;
        }
    }
}