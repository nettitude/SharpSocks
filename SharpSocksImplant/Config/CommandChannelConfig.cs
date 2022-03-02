namespace SharpSocksImplant.Config
{
    public class CommandChannelConfig
    {
        private readonly object _locker = new object();
        private string _commandChannelSessionId;
        public short commandBeaconTime = 5000;

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