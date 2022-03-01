using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Text;

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

        public bool FailError(string message, Guid errorCode)
        {
            Console.WriteLine($"[{DateTime.Now}][-] Error Code: {errorCode} + Message: {message} ");
            throw new Exception(message ?? "");
        }

        public void BannerMessage(string message)
        {
            Console.WriteLine(message);
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

        public void HexDump(byte[] data, int width = 16)
        {
            var ctr = 0;
            data.Select((b, i) => new
            {
                Byte = b,
                Index = i
            }).GroupBy(o => o.Index / width).Select(g => g.Aggregate(new
            {
                Hex = new StringBuilder(),
                Chars = new StringBuilder()
            }, (a, o) =>
            {
                a.Hex.AppendFormat("{0:X2} ", o.Byte);
                a.Chars.Append(Convert.ToChar(o.Byte));
                return a;
            }, a => new
            {
                Index = (ctr++ * width).ToString("X4"),
                Hex = a.Hex.ToString(),
                Chars = a.Chars.ToString()
            })).ToList().ForEach(y => Console.WriteLine($"{y.Index} {y.Hex.ToString().ToLower()} {ToLiteral(y.Chars)}"));
        }

        private static string ToLiteral(string input)
        {
            using var stringWriter = new StringWriter();
            using var provider = CodeDomProvider.CreateProvider("CSharp");
            provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), stringWriter, null);
            var charArray = stringWriter.ToString().Replace("\"", "").ToCharArray();
            var stringBuilder = new StringBuilder();
            for (var index = 0; index < charArray.Length; ++index)
                if (index + 1 < charArray.Length && charArray[index] == '\\' && char.IsLetter(charArray[index + 1]))
                {
                    stringBuilder.Append(charArray[index++]);
                    stringBuilder.Append(charArray[index]);
                }
                else
                {
                    stringBuilder.Append(" ");
                    stringBuilder.Append(charArray[index]);
                }

            return stringBuilder.ToString();
        }
    }
}