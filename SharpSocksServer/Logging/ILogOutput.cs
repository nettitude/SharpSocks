using System;

namespace SharpSocksServer.Logging
{
    public interface ILogOutput
    {
        void LogError(string errorMessage);

        void LogError(Exception errorMessage);

        void LogMessage(string message);

        void LogImportantMessage(string message);

        bool IsVerboseOn();
    }
}