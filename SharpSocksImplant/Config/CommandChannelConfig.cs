using System;

namespace SharpSocksImplant.Config
{
    public class CommandChannelConfig
    {
        private readonly object _locker = new object();
        private string _commandChannelSessionId;
        public short CommandBeaconTime = 5000;
        public Action CommandChannelSessionIdChanged;
        public decimal CommandJitter = 0.20M;
        public short CommandTimeoutRetryAttempts = 10;
        public short CommandTimeoutRetryOnFailure = 15000;

        public string CommandChannelSessionId
        {
            get => _commandChannelSessionId;
            set
            {
                lock (_locker)
                {
                    _commandChannelSessionId = value;
                }
            }
        }
    }
}