using System;

namespace SharpSocksImplant.Logging
{
    public interface IImplantLog
    {
        void LogError(string errorMessage);

        void LogMessage(string message);

        void LogImportantMessage(string message);

        bool FailError(string message, Guid errorCode);

        void BannerMessage(string message);

        void SetVerboseOn();

        void SetVerboseOff();

        bool IsVerboseOn();
    }
}