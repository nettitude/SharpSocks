
using Common.Server.Interfaces;
using System;
using System.Collections.Generic;

namespace SharpSocksServer.ServerComms
{
    public class DebugConsoleOutput : ILogOutput
    {
        bool _verbose = false;
        public List<byte> SendToServer(List<byte> payload)
        {
            return new List<Byte>();
        }

        public bool AskQuestion(String question)
        {
            Console.WriteLine(question);
            return (Console.ReadLine().ToLower() == "y");
        }
        public void LogError(String errorMesg) { Console.WriteLine($"[{DateTime.Now}][X] {errorMesg}"); }
        public void LogMessage(String mesg) { Console.WriteLine($"[{DateTime.Now}][!] {mesg}"); }

        public bool FailError(String mesg, Guid ErrorCode)
        {
            Console.WriteLine($"[{DateTime.Now}][X] Error Code: {ErrorCode} Message: {mesg} ");
            throw new Exception($"{mesg}");
        }

        public void BannerMesg(String mesg)
        {
            Console.WriteLine(mesg);
        }

        public void SetVerboseOn() { _verbose = true; }
        public void SetVerboseOff() { _verbose = false; }
        public bool IsVerboseOn() { return _verbose; }
    }
}
