using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImplantSide.Classes.Config
{
    public class CommandChannelConfig
    {
        object _locker = new object();
        String _commandChannelSessionId;

        public Action CommandChannelSessionIdChanged;
        public Int16 CommandBeaconTime = 5000;
        public Decimal CommandJitter = 0.20M;
        public Int16 CommandTimeoutRetryOnFailure = 15000;
        public Int16 CommandTimeoutRetryAttempts = 10;
        public String CommandChannelSessionId
        {
            get
            {
                return _commandChannelSessionId;
            }
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
