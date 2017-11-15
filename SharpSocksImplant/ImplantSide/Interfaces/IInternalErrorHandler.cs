using System.Collections.Generic;

namespace ImplantSide.Interfaces
{
    public interface IInternalErrorHandler
    {
        void FailError(List<string> errors);
        void FailError(string error);
        void LogError(List<string> errors);
        void LogError(string error);
    }
}