using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using SharpSocksImplant.Logging;

namespace SharpSocksImplant.Config
{
    public class SocksClientConfiguration
    {
        private const string DEFAULT_SESSION_COOKIE_NAME = "ASP.NET_SessionId";
        private readonly object _locker = new object();
        private Uri _serverUri;
        private string _sessionId;
        public CommandChannelConfig commandChannel;

        public string CommandChannelSessionId
        {
            get => commandChannel != null ? commandChannel.CommandChannelSessionId : Guid.Empty.ToString();
            set => GetCmdChannelConfig().CommandChannelSessionId = value;
        }

        public short BeaconTime
        {
            get => GetCmdChannelConfig().CommandBeaconTime;
            set => GetCmdChannelConfig().CommandBeaconTime = value;
        }

        public string ServerCookie { get; set; }

        public string PayloadCookieName { get; set; }

        public string HostHeader { get; set; }

        public Uri CommandServerUi
        {
            get
            {
                Monitor.Enter(_locker);
                var serverUri = _serverUri;
                Monitor.Exit(_locker);
                return serverUri;
            }
            set
            {
                Monitor.Enter(_locker);
                _serverUri = value;
                Monitor.Exit(_locker);
            }
        }

        public bool InsecureSSL { get; set; }

        public string ServerHost { get; set; }

        public bool UseProxy { get; set; }

        public string UserAgent { get; set; }

        public IWebProxy WebProxy { get; set; }

        public IImplantLog ImplantComms { get; set; }

        public ITamper Tamper { get; set; }

        public List<string> UrlPaths { get; set; }

        public ushort TimeBetweenReads { get; set; }

        internal Uri Url
        {
            get
            {
                Monitor.Enter(_locker);
                var serverUri = _serverUri;
                Monitor.Exit(_locker);
                return serverUri;
            }
        }

        public string SessionCookieName
        {
            get => !string.IsNullOrWhiteSpace(_sessionId) ? _sessionId : DEFAULT_SESSION_COOKIE_NAME;
            set => _sessionId = value;
        }

        private CommandChannelConfig GetCmdChannelConfig()
        {
            if (commandChannel == null)
                lock (_locker)
                {
                    if (commandChannel == null)
                        commandChannel = new CommandChannelConfig();
                }

            return commandChannel;
        }
    }
}