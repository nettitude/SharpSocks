using System;
using System.Security;


namespace SharpSocksImplantTestApp.Helper
{
    public static class ConsolePassword
    {

        public static SecureString ReadPasswordFromConsole()
        {
            ConsoleKeyInfo key;
            var password = new SecureString();
            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password.AppendChar(key.KeyChar);
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        password.RemoveAt(password.Length - 1);
                        Console.Write("\b \b");
                    }
                }
            } while (key.Key != ConsoleKey.Enter);

            return password;
        }
    }
}
