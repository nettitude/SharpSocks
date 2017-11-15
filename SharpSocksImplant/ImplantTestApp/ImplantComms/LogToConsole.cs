using ImplantSide.Interfaces;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Text;


namespace SharpSocksImplantTestApp.ImplantComms
{
    public class LogToConsole : IImplantLog
    {
        bool _verbose = false;
        public void LogError(String errorMesg) { Console.WriteLine($"[{DateTime.Now}][X]: {errorMesg}"); }
        public void LogMessage(String mesg) { Console.WriteLine($"[{DateTime.Now}][!]: {mesg}"); }

        public bool FailError(String mesg, Guid ErrorCode)
        {
            Console.WriteLine($"[{DateTime.Now}][X] Error Code: {ErrorCode} + Message: {mesg} ");
            throw new Exception($"{mesg}");
        }

        public void BannerMesg(String mesg)
        {
            Console.WriteLine(mesg);
        }

        public void HexDump(byte[] data, int width = 16)
        {
            var ctr = 0;
            data.Select((b, i) => new { Byte = b, Index = i })
                .GroupBy(o => o.Index / width)
                .Select(g =>
                    g
                    .Aggregate(
                        new { Hex = new StringBuilder(), Chars = new StringBuilder() },
                        (a, o) => { a.Hex.AppendFormat("{0:X2} ", o.Byte); a.Chars.Append(Convert.ToChar(o.Byte)); return a; },
                        a => new { Index = (ctr++ * width).ToString("X4"), Hex = a.Hex.ToString(), Chars = a.Chars.ToString() }
                    )
                )
                .ToList().ForEach(y =>
               {
                   Console.WriteLine($"{y.Index} {y.Hex.ToString().ToLower()} {ToLiteral(y.Chars)}");
               });
        }

        static string ToLiteral(string input)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                    var result = writer.ToString();
                    result = result.Replace("\"", "");
                    var chars = result.ToCharArray();
                    var sb = new StringBuilder();
                    for ( var i = 0; i < chars.Length; i++ )
                    {
                        if ((((i + 1) < chars.Length)) && (chars[i] == '\\' && Char.IsLetter(chars[i + 1])))
                        {
                            sb.Append(chars[i++]);
                            sb.Append(chars[i]);
                        }
                        else
                        {
                            sb.Append(" ");
                            sb.Append(chars[i]);
                        }
                    }
                   
                    return sb.ToString();
                }
            }
        }

        public void SetVerboseOn() { _verbose = true; }
        public void SetVerboseOff() { _verbose = false; }
        public bool IsVerboseOn() { return _verbose; }
    }
}
