using System;
using SharpSocksServer.Config;

namespace SharpSocksServer.Logging
{
    public class ConsoleOutput : ILogOutput
    {
        private readonly bool _verbose;

        public ConsoleOutput(SharpSocksConfig config)
        {
            _verbose = config.Verbose;
        }

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
            Console.WriteLine($"[{DateTime.Now}][!][!] {message} [!][!]");
        }

        public bool IsVerboseOn()
        {
            return _verbose;
        }
    }
}