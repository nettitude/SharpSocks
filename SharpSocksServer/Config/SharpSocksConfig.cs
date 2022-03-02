using System;
using System.Security.Cryptography;
using McMaster.Extensions.CommandLineUtils;
using SharpSocksServer.Logging;

namespace SharpSocksServer.Config
{
    public class SharpSocksConfig
    {
        private const string DEFAULT_COMMAND_CHANNEL_ID = "7f404221-9f30-470b-b05d-e1a922be3ff6";

        public string SocksIP { get; private init; }
        public ushort SocksPort { get; private init; }
        public string SessionCookieName { get; private init; }
        public string PayloadCookieName { get; private init; }
        public uint SocketTimeout { get; private init; }
        public bool Verbose { get; private init; }
        public string HttpServerURI { get; private init; }
        public string EncryptionKey { get; private init; }
        public string CommandChannelId { get; private init; }
        public ushort CommandLimit { get; private init; }
        public bool WaitOnConnect { get; private init; }

        private static string ValidateHttpServer(ILogOutput logger, string serverUri)
        {
            if (string.IsNullOrWhiteSpace(serverUri))
            {
                logger.LogMessage("URI to listen is blank defaulting to http://127.0.0.1:8081");
                serverUri = "http://127.0.0.1:8081";
            }

            if (serverUri[^1] != '/')
                serverUri += "/";
            return serverUri;
        }

        private static string ValidateCmdChannelId(ILogOutput logger, string commandChannelId)
        {
            if (!string.IsNullOrWhiteSpace(commandChannelId)) return commandChannelId;
            logger.LogMessage($"Command Channel Id is blank defaulting to {DEFAULT_COMMAND_CHANNEL_ID}");
            return string.IsNullOrWhiteSpace(commandChannelId) ? DEFAULT_COMMAND_CHANNEL_ID : commandChannelId;
        }

        private static string ValidateEncryptionKey(ILogOutput logger, string encryptionKey)
        {
            if (!string.IsNullOrWhiteSpace(encryptionKey))
                return encryptionKey;
            var aes = Aes.Create();
            aes.GenerateKey();
            var base64String = Convert.ToBase64String(aes.Key);
            logger.LogMessage($"Using encryption key (base64'd) {base64String}");
            return base64String;
        }

        public static SharpSocksConfig LoadConfig(ILogOutput logger, CommandOption optSocksServerUri, CommandOption optSocketTimeout, CommandOption optCmdChannelId,
            CommandOption optEncKey, CommandOption optSessionCookie, CommandOption optPayloadCookie, CommandOption optVerbose, CommandOption optHttpServer)
        {
            var socksHostPort = !optSocksServerUri.HasValue() || string.IsNullOrWhiteSpace(optSocksServerUri.Value()) ? "*:43334" : optSocksServerUri.Value();
            if (string.IsNullOrEmpty(socksHostPort) || !socksHostPort.Contains(":"))
            {
                throw new Exception($"Socks IP not in {socksHostPort} IP:port format");
            }

            string socksPortString = null;
            string socksIpToListen = null;
            var socksHostPortSplit = socksHostPort.Split(':');
            if (socksHostPortSplit.Length > 1)
            {
                socksIpToListen = socksHostPortSplit[0];
                socksPortString = socksHostPortSplit[1];
            }

            if (!ushort.TryParse(socksPortString, out var socksPort) && socksPort < 1024)
            {
                throw new Exception($"[!] Port [{socksPortString}] is not valid (or is less than 1024)");
            }

            uint timeout = 30;
            var convertedSuccessfully = optSocketTimeout.HasValue() && uint.TryParse(optSocketTimeout.Value(), out timeout);
            if (!convertedSuccessfully)
            {
                timeout = 30U;
                logger.LogMessage($"Defaulting Socket Timeout to {timeout}s");
            }

            timeout *= 1000U;

            return new SharpSocksConfig
            {
                SocksIP = socksIpToListen,
                SocksPort = socksPort,
                CommandChannelId = ValidateCmdChannelId(logger, optCmdChannelId.Value()),
                EncryptionKey = ValidateEncryptionKey(logger, optEncKey.Value()),
                SessionCookieName = optSessionCookie.Value() ?? "ASP.NET_SessionId",
                PayloadCookieName = optPayloadCookie.Value() ?? "__RequestVerificationToken",
                SocketTimeout = timeout,
                Verbose = optVerbose.HasValue(),
                HttpServerURI = ValidateHttpServer(logger, optHttpServer.Value()),
                WaitOnConnect = true,
                CommandLimit = 20
            };
        }
    }
}