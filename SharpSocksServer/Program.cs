using System;
using McMaster.Extensions.CommandLineUtils;
using SharpSocksCommon.Encryption;
using SharpSocksServer.Config;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;
using SharpSocksServer.SocksServer;

namespace SharpSocksServer
{
    internal static class Program
    {
        private static readonly ConsoleOutput LOGGER = new();
        private static CommandLineApplication _app;

        private static void Main(string[] args)
        {
            Console.WriteLine("SharpSocks Server\r\n=================\n");
            try
            {
                var config = Init(args);

                if (config.Verbose)
                    LOGGER.SetVerboseOn();

                SocksProxy.Logger = LOGGER;

                var cryptor = new RijndaelCBCCryptor(config.EncryptionKey);
                LOGGER.LogMessage("Using Rijndael CBC encryption");

                var requestProcessor = new EncryptedC2RequestProcessor(cryptor, config.SessionCookieName, config.CommandChannelId, 20)
                {
                    Logger = LOGGER,
                    PayloadCookieName = config.PayloadCookieName
                };

                var serverController = new SharpSocksServerController
                {
                    Logger = LOGGER,
                    WaitOnConnect = true,
                    SocketTimeout = config.SocketTimeout,
                    RequestProcessor = requestProcessor
                };

                serverController.StartHttp(config.HttpServerURI);
                serverController.StartSocks(config.SocksIP, config.SocksPort);

                LOGGER.LogMessage("Press x to quit\r\n");
                while ("x" != Console.ReadLine())
                {
                }
            }
            catch (Exception e)
            {
                LOGGER.LogError(e);
            }
        }

        private static SharpSocksConfig Init(string[] args)
        {
            SharpSocksConfig config = null;
            _app = new CommandLineApplication();
            _app.HelpOption();
            var optSocksServerUri = _app.Option("-s|--socksserveruri", "IP:Port for SOCKS to listen on, default is *:43334", CommandOptionType.SingleValue);
            var optCmdChannelId = _app.Option("-c|--cmdid", "Command Channel Identifier, needs to be shared with the server", CommandOptionType.SingleValue);
            var optHttpServer = _app.Option("-l|--httpserveruri", "Uri to listen on, default is http://127.0.0.1:8081", CommandOptionType.SingleValue);
            var optEncKey = _app.Option("-k|--encryptionkey", "The encryption key used to secure comms", CommandOptionType.SingleValue);
            var optSessionCookie = _app.Option("-sc|--sessioncookie", "The name of the cookie to pass the session identifier", CommandOptionType.SingleValue);
            var optPayloadCookie = _app.Option("-pc|--payloadcookie", "The name of the cookie to pass smaller requests through", CommandOptionType.SingleValue);
            var optSocketTimeout = _app.Option("-st|--socketTimeout", "How long should SOCKS sockets be held open for, default is 30s", CommandOptionType.SingleValue);
            var optVerbose = _app.Option("-v|--verbose", "Verbose error logging", CommandOptionType.NoValue);
            _app.OnExecute(() =>
            {
                config = SharpSocksConfig.LoadConfig(LOGGER, optSocksServerUri, optSocketTimeout, optCmdChannelId, optEncKey, optSessionCookie, optPayloadCookie,
                    optVerbose, optHttpServer);
            });
            _app.Execute(args);
            return config;
        }
    }
}