using ImplantSide.Interfaces;
using System;
using System.Collections.Generic;

namespace SocksProxy.Classes.Integration
{
    public class PoshDefaultImplantComms : IImplantLog
    {
        bool _verbose = false;
        public Action<String> _LogError { get; set; }
        public Action<String> _LogMessage { get; set; }
        public Func<String, Guid, bool> _FailError { get; set; }
        public Action<String> _BannerMesg { get; set; }
                
        public void LogError(String errorMesg)
        {
            _LogError?.Invoke(errorMesg);
        }

        public void LogMessage(String mesg)
        {
            _LogMessage?.Invoke(mesg);
        }

        public bool FailError(String mesg, Guid ErrorCode)
        {
            if (null != _FailError)
                return _FailError(mesg, ErrorCode);
            else
                return false;
        }

        public void BannerMesg(String mesg)
        {
            _BannerMesg?.Invoke(mesg);
        }

        public void SetVerboseOn() { _verbose = true; }
        public void SetVerboseOff() { _verbose = false; }
        public bool IsVerboseOn() { return _verbose; }
    }
}
