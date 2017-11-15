using ImplantSide.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSocksServer.ServerComms
{
    public class DebugFileLog : IImplantLog
    {
        String FileName = null;
        public String LogFilePath { get { return FileName; } }
        object _locker = new object();

        public DebugFileLog()
        {
            var LogPath = Path.GetTempPath();

            if (!Directory.Exists(LogPath))
            throw new Exception($"ERROR: Log directory {LogPath} does not exist");

            FileName = Path.Combine(LogPath, $"SharpSocks_Log_{DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")}.txt");
        }

        public DebugFileLog(String LogPath)
        {
            if (!Directory.Exists(LogPath))
                throw new Exception($"ERROR: Log directory {LogPath} does not exist");

            FileName = Path.Combine(LogPath, $"SharpSocks_Log_{DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")}.txt");
        }
        bool _verbose = false;

        void WriteToFile(String text)
        {
            lock (_locker)
            {
                Stream logfile = null;
                try
                {
                    if (!File.Exists(FileName))
                        logfile = File.Open(FileName, FileMode.OpenOrCreate, FileAccess.Write);
                    else
                        logfile = File.Open(FileName, FileMode.Append, FileAccess.ReadWrite);

                    using (var sw = new StreamWriter(logfile))
                    {
                        sw.WriteLine(text);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"ERROR: Unable to write to {FileName}: {ex.Message}");
                }
                finally
                {
                    if (null != logfile)
                    {
                        logfile.Close();
                        logfile = null;
                    }
                }
            }
        }

        public void LogError(String errorMesg)
        {
            lock (_locker)
            {

                WriteToFile($"[{DateTime.Now}][X] {errorMesg}");
            }
        }
        public void LogMessage(String mesg)
        {
            WriteToFile($"[{DateTime.Now}][!] {mesg}");
        }

        public bool FailError(String mesg, Guid ErrorCode)
        {
            WriteToFile($"[{DateTime.Now}][X] Error Code: {ErrorCode} Message: {mesg} ");
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

