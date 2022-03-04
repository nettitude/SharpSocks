﻿using System;
using System.Security.Cryptography;
using McMaster.Extensions.CommandLineUtils;
using SharpSocksServer.Integration;
using SharpSocksServer.Logging;

namespace SharpSocksServer
{
    internal static class Program
    {
        private const string DEFAULT_COMMAND_CHANNEL_ID = "7f404221-9f30-470b-b05d-e1a922be3ff6";
        private static readonly ConsoleOutput LOGGER = new();
        private static CommandLineApplication _app;

        private static void Main(string[] args)
        {
            try
            {
                StartSocksServer(args);
            }
            catch (Exception e)
            {
                LOGGER.LogError(e);
            }
        }

        private static void StartSocksServer(string[] args)
        {
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
                var socksPortString = "<blank>";
                string socksIpToListen = null;
                var socksHostPort = !optSocksServerUri.HasValue() || string.IsNullOrWhiteSpace(optSocksServerUri.Value()) ? "*:43334" : optSocksServerUri.Value();
                if (string.IsNullOrEmpty(socksHostPort) || !socksHostPort.Contains(":"))
                {
                    LOGGER.LogMessage($"Socks IP not in {socksHostPort} IP:port format");
                    return;
                }

                var socksHostPortSplit = socksHostPort.Split(':');
                if (socksHostPortSplit.Length > 1)
                {
                    socksIpToListen = socksHostPortSplit[0];
                    socksPortString = socksHostPortSplit[1];
                }

                if (!ushort.TryParse(socksPortString, out var socksPort) && socksPort < 1024)
                {
                    LOGGER.LogMessage($"[!] Port [{socksPortString}] is not valid (or is less than 1024)");
                    LOGGER.LogMessage(_app.GetHelpText());
                    return;
                }

                uint timeout = 30;
                var convertedSuccessfully = optSocketTimeout.HasValue() && uint.TryParse(optSocketTimeout.Value(), out timeout);
                if (!convertedSuccessfully)
                {
                    timeout = 30U;
                    LOGGER.LogMessage($"Defaulting Socket Timeout to {timeout}s");
                }

                timeout *= 1000U;
                if (optVerbose.HasValue())
                    LOGGER.SetVerboseOn();
                StartSocks(socksIpToListen, ValidateServerUri(optHttpServer.Value()), ValidateCmdChannelId(optCmdChannelId.Value()), socksPort,
                    ValidateEncryptionKey(optEncKey.Value()), optSessionCookie.Value(), optPayloadCookie.Value(), timeout);
            });
            try
            {
                _app.Execute(args);
            }
            catch (Exception e)
            {
                LOGGER.LogError(e);
            }
        }

        private static string ValidateServerUri(string serverUri)
        {
            if (!string.IsNullOrWhiteSpace(serverUri)) return serverUri;
            LOGGER.LogMessage("URI to listen is blank defaulting to http://127.0.0.1:8081");
            if (string.IsNullOrWhiteSpace(serverUri))
                serverUri = "http://127.0.0.1:8081";
            return serverUri;
        }

        private static string ValidateCmdChannelId(string commandChannelId)
        {
            if (!string.IsNullOrWhiteSpace(commandChannelId)) return commandChannelId;
            LOGGER.LogMessage($"Command Channel Id is blank defaulting to {DEFAULT_COMMAND_CHANNEL_ID}");
            return string.IsNullOrWhiteSpace(commandChannelId) ? DEFAULT_COMMAND_CHANNEL_ID : commandChannelId;
        }

        private static string ValidateEncryptionKey(string encryptionKey)
        {
            if (!string.IsNullOrWhiteSpace(encryptionKey))
                return encryptionKey;
            var aes = Aes.Create();
            aes.GenerateKey();
            var base64String = Convert.ToBase64String(aes.Key);
            LOGGER.LogMessage($"Using encryption key (base64'd) {base64String}");
            return base64String;
        }

        private static void StartSocks(string socksIpToListen, string serverUri, string commandChannelId, ushort socksPort, string encryptionKey, string sessionCookieName,
            string payloadCookieName, uint socketTimeout, bool help = false)
        {
            Banner();
            if (help)
            {
                LOGGER.LogMessage(_app.GetHelpText());
            }
            else
            {
                LOGGER.LogMessage("[x] to quit\r\n");

                PsSocksServer.CreateSocksController(socksIpToListen, serverUri, null, commandChannelId, socksPort, encryptionKey, sessionCookieName, payloadCookieName, LOGGER,
                    socketTimeout);
                string input;
                while ("x" != (input = Console.ReadLine()))
                    if (!string.IsNullOrEmpty(input) && input.StartsWith("LPoll="))
                    {
                        var strArray = input.Split('=');
                        if (strArray.Length > 1 && int.TryParse(strArray[1], out var result))
                            PsSocksServer.SetLongPollTimeout(result);
                        else
                            LOGGER.LogMessage("[X] New Long Poll format ");
                    }
            }
        }

        private static void Banner()
        {
            ConsoleOutput.BannerMessage("SharpSocks Server\r\n=================\n");
        }
    }
}