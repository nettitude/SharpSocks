using System;
using System.Linq;
using System.Net;

namespace SharpSocksImplant.Utils
{
    public static class WebExceptionAnalyzer
    {
        public static bool IsTransient(Exception ex)
        {
            if (ex is WebException webException)
                if (new[]
                {
                    WebExceptionStatus.ConnectionClosed,
                    WebExceptionStatus.Timeout,
                    WebExceptionStatus.RequestCanceled,
                    WebExceptionStatus.ReceiveFailure
                }.Contains(webException.Status))
                    return true;
            return false;
        }
    }
}