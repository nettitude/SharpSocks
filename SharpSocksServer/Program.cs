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
                Console.WriteLine("[*] Initialising...");
                var config = ParseArgs(args);

                var services = new ServiceCollection()
                    .AddSingleton(config)
                    .AddSingleton<IEncryptionHelper>(new RijndaelCBCCryptor(config.EncryptionKey))
                    .AddSingleton<EncryptedC2RequestProcessor>()
                    .AddSingleton<HttpServerController>()
                    .AddSingleton<ServerController>()
                    .AddSingleton<ILogOutput, ConsoleOutput>();

                var serviceProvider = services.BuildServiceProvider();

                var logger = serviceProvider.GetRequiredService<ILogOutput>();
                SocksProxy.Logger = logger;

                var httpServerController = serviceProvider.GetRequiredService<HttpServerController>();
                var serverController = serviceProvider.GetRequiredService<ServerController>();
                Console.WriteLine("[*] Initialised");

                httpServerController.StartHttp();
                serverController.StartSocks();

                logger.LogImportantMessage("Press x to quit");

                while ("x" != Console.ReadLine())
                {
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Fatal Error: {e}");
            }
        }

        private static SharpSocksConfig ParseArgs(string[] args)
        {
            var _app = new CommandLineApplication();
            _app.HelpOption();
            var optSocksServerUri = _app.Option("-s|--socksserveruri", "IP:Port for SOCKS to listen on, default is *:43334", CommandOptionType.SingleValue);
            var optCmdChannelId = _app.Option("-c|--cmdid", "Command Channel Identifier, needs to be shared with the server", CommandOptionType.SingleValue);
            var optHttpServer = _app.Option("-l|--httpserveruri", "Uri to listen on, default is http://127.0.0.1:8081", CommandOptionType.SingleValue);
            var optEncKey = _app.Option("-k|--encryptionkey", "The encryption key used to secure comms", CommandOptionType.SingleValue);
            var optSessionCookie = _app.Option("-sc|--sessioncookie", "The name of the cookie to pass the session identifier", CommandOptionType.SingleValue);
            var optPayloadCookie = _app.Option("-pc|--payloadcookie", "The name of the cookie to pass smaller requests through", CommandOptionType.SingleValue);
            var optSocketTimeout = _app.Option("-st|--socketTimeout", "How long should SOCKS sockets be held open for, default is 30s", CommandOptionType.SingleValue);
            var optVerbose = _app.Option("-v|--verbose", "Verbose error logging", CommandOptionType.NoValue);

            SharpSocksConfig config = null;
            _app.OnExecute(() =>
            {
                config = SharpSocksConfig.LoadConfig(optSocksServerUri, optSocketTimeout, optCmdChannelId, optEncKey, optSessionCookie, optPayloadCookie,
                    optVerbose, optHttpServer);
            });
            _app.Execute(args);
            return config;
        }
    }
}