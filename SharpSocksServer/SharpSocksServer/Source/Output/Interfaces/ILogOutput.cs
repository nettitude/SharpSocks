using System;
using System.Collections.Generic;

namespace Common.Server.Interfaces
{
    public interface ILogOutput
    {
        void LogError(String errorMesg);
        void LogMessage(String mesg);
        bool FailError(String mesg, Guid ErrorCode);
        void BannerMesg(String mesg);
        void SetVerboseOn();
        void SetVerboseOff();
        bool IsVerboseOn();
    }
}
