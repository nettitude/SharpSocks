using System;
using System.Security;

namespace SharpSocksImplant.Utils
{
    public static class ConsolePassword
    {
        public static SecureString ReadPasswordFromConsole()
        {
            var secureString = new SecureString();
            ConsoleKeyInfo consoleKeyInfo;
            do
            {
                consoleKeyInfo = Console.ReadKey(true);
                if (consoleKeyInfo.Key != ConsoleKey.Backspace && consoleKeyInfo.Key != ConsoleKey.Enter)
                {
                    secureString.AppendChar(consoleKeyInfo.KeyChar);
                    Console.Write("*");
                }
                else if (consoleKeyInfo.Key == ConsoleKey.Backspace && secureString.Length > 0)
                {
                    secureString.RemoveAt(secureString.Length - 1);
                    Console.Write("\b \b");
                }
            } while (consoleKeyInfo.Key != ConsoleKey.Enter);

            return secureString;
        }
    }
}