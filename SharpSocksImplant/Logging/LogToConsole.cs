using System;

namespace SharpSocksImplant.Logging
{
    public class LogToConsole : IImplantLog
    {
        private bool _verbose;

        public void LogError(string errorMessage)
        {
            Console.WriteLine($"[{DateTime.Now}][-]: {errorMessage}");
        }

        public void LogMessage(string message)
        {
            if (_verbose)
            {
                Console.WriteLine($"[{DateTime.Now}][*]: {message}");
            }
        }

        public void LogImportantMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now}][!]: {message}");
        }

        public void BannerMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void SetVerboseOn()
        {
            _verbose = true;
        }
    }
}