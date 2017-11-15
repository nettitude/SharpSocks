using Common.Encryption.Debug;
using Common.Server.Interfaces;
using SharpSocksServer.ServerComms;
using SharpSocksServer.SharpSocksServer.Classes;
using SocksServer.Classes.Server;
using SocksTunnel.Classes;
using System;
using System.Collections.Generic;

namespace SharpSocksServer.Source.Integration
{
    public static class PSSocksServer
    {
        public static SharpSocksServerController CreateSocksController(String ipToListen, String serverUri, String commandChannelId, ushort SocksPort, String EncryptionKey, String SessionCookieName, String PayloadCookieName, ILogOutput logComms)
        {
            var logOutput = logComms ?? new DebugConsoleOutput();
            var mstr = new SharpSocksServerController() { ServerComms = logComms, WaitOnConnect = true };
            var enc = new DebugSimpleEncryptor(EncryptionKey);
            logComms.LogMessage($"Public key for {enc.Initialize()}");

            //Step 1. Create the HTTP request processor
            //This will parse whatever the implant has sent
            var c2Processor = new EncryptedC2RequestProcessor(enc, SessionCookieName ?? "ASP.NET_SessionId", commandChannelId)
            {
                ServerComms = logComms,
                PayloadCookieName = PayloadCookieName ?? "__RequestVerificationToken"
            };
            SocksProxy.SocketComms = c2Processor;

            //Step 2. Setup the Listener
            //Prefix must end in /
            if (serverUri[serverUri.Length - 1] != '/')
                serverUri += "/";
            var httpAsync = new HttpAsyncListener(c2Processor);
            httpAsync.CreateListener(new List<String>() { serverUri });
            logOutput.LogMessage($"C2 HTTP processor listening on {serverUri}");

            //Step 3. Start the Socks Proxy
            //TO DO: Hardcoded socks IP and Port change this!!!
            mstr.StartSocks(ipToListen, SocksPort, httpAsync, c2Processor.CmdChannelRunningEvent);

            return mstr;

        }
    }
}
