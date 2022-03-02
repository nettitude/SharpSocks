namespace SharpSocksImplant.Logging
{
    public interface IImplantLog
    {
        void LogError(string errorMessage);

        void LogMessage(string message);

        void LogImportantMessage(string message);

        void BannerMessage(string message);

        void SetVerboseOn();
    }
}