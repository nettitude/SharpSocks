using System;
using SharpSocksImplant.Logging;

namespace SharpSocksImplant.Integration
{
    public class PoshDefaultImplantComms : IImplantLog
    {
        private bool _verbose;

        public Action<string> _LogError { get; set; }
        public Action<string> _LogMessage { get; set; }
        public Action<string> _LogImportantMessage { get; set; }

        public Func<string, Guid, bool> _FailError { get; set; }

        public Action<string> _BannerMesg { get; set; }

        public void LogError(string errorMessage)
        {
            _LogError?.Invoke(errorMessage);
        }

        public void LogMessage(string message)
        {
            if (_verbose)
            {
                _LogMessage?.Invoke(message);
            }
        }

        public void LogImportantMessage(string message)
        {
            _LogImportantMessage?.Invoke(message);
        }

        public bool FailError(string message, Guid errorCode)
        {
            return _FailError != null && _FailError(message, errorCode);
        }

        public void BannerMessage(string message)
        {
            var bannerMessage = _BannerMesg;
            bannerMessage?.Invoke(message);
        }

        public void SetVerboseOn()
        {
            _verbose = true;
        }

        public void SetVerboseOff()
        {
            _verbose = false;
        }

        public bool IsVerboseOn()
        {
            return _verbose;
        }
    }
}