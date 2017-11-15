using System;

namespace ImplantSide.Interfaces
{
    public interface IImplantLog
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
