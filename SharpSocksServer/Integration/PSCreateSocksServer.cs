using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using SharpSocksCommon.Encryption;
using SharpSocksServer.ImplantComms;
using SharpSocksServer.Logging;
using SharpSocksServer.SocksServer;

namespace SharpSocksServer.Integration
{
    public static class PsSocksServer
    {
        public static void CreateSocksController(string ipToListen, string serverUri, X509Certificate2 serverCert, string commandChannelId, ushort socksPort,
            string encryptionKey, string sessionCookieName, string payloadCookieName, ILogOutput logger, uint socketTimeout = 300000)
        {
            var logOutput = logger ?? new ConsoleOutput();
            var serverController = new SharpSocksServerController
            {
                Logger = logger,
                WaitOnConnect = true,
                SocketTimeout = socketTimeout
            };
            var cryptor = new RijndaelCBCCryptor(encryptionKey);
            logger?.LogMessage("Using Rijndael CBC encryption");
            var requestProcessor = new EncryptedC2RequestProcessor(cryptor, sessionCookieName ?? "ASP.NET_SessionId", commandChannelId, 20)
            {
                Logger = logger,
                PayloadCookieName = payloadCookieName ?? "__RequestVerificationToken"
            };
            SocksProxy.SocketComms = requestProcessor;
            if (serverUri[^1] != '/')
                serverUri += "/";
            var httpAsyncListener = new HttpAsyncListener(requestProcessor, logOutput);
            httpAsyncListener.CreateListener(new Dictionary<string, X509Certificate2>
            {
                {
                    serverUri,
                    serverCert
                }
            });
            logOutput.LogMessage($"C2 HTTP processor listening on {serverUri}");
            serverController.StartSocks(ipToListen, socksPort, requestProcessor.CmdChannelRunningEvent);
        }

        public static void SetLongPollTimeout(int timeout)
        {
            if (SocksProxy.SocketComms is not EncryptedC2RequestProcessor socketComms)
                return;
            socketComms.LongPollTimeout = timeout;
        }
    }
}