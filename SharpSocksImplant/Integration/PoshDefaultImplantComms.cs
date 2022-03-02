using System;
using SharpSocksImplant.Logging;

namespace SharpSocksImplant.Integration
{
    public class PoshDefaultImplantComms : IImplantLog
    {
        private bool _verbose;

        public PoshDefaultImplantComms(Action<string> logErrorAction, Action<string> logMessageAction, Action<string> logImportantMessageAction, Action<string> bannerMessageAction)
        {
            LogErrorAction = logErrorAction;
            LogMessageAction = logMessageAction;
            LogImportantMessageAction = logImportantMessageAction;
            BannerMessageAction = bannerMessageAction;
        }

        private Action<string> LogErrorAction { get; }
        private Action<string> LogMessageAction { get; }
        private Action<string> LogImportantMessageAction { get; }

        private Action<string> BannerMessageAction { get; }

        public void LogError(string errorMessage)
        {
            LogErrorAction?.Invoke(errorMessage);
        }

        public void LogMessage(string message)
        {
            if (_verbose)
            {
                LogMessageAction?.Invoke(message);
            }
        }

        public void LogImportantMessage(string message)
        {
            LogImportantMessageAction?.Invoke(message);
        }

        public void BannerMessage(string message)
        {
            BannerMessageAction?.Invoke(message);
        }

        public void SetVerboseOn()
        {
            _verbose = true;
        }
    }
}