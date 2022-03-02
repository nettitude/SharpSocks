using System;

namespace SharpSocksServer.Logging
{
    public class ConsoleOutput : ILogOutput
    {
        private bool _verbose;

        public void LogError(string errorMessage)
        {
            Console.WriteLine($"[{DateTime.Now}][X] {errorMessage}");
        }

        public void LogError(Exception errorMessage)
        {
            Console.WriteLine($"[{DateTime.Now}][X] {errorMessage}");
        }

        public void LogMessage(string message)
        {
            if (_verbose)
            {
                Console.WriteLine($"[{DateTime.Now}][*] {message}");
            }
        }

        public void LogImportantMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now}][!] {message}");
        }

        public bool IsVerboseOn()
        {
            return _verbose;
        }

        public static void BannerMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void SetVerboseOn()
        {
            _verbose = true;
        }
    }
}