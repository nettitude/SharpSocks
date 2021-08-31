using System;

namespace SharpSocksServer.Logging
{
    public interface ILogOutput
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