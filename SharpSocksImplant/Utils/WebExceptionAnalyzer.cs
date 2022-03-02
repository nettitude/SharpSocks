using System;
using System.Linq;
using System.Net;

namespace SharpSocksImplant.Utils
{
    public static class WebExceptionAnalyzer
    {
        public static bool IsTransient(Exception e)
        {
            if (!(e is WebException webException)) return false;
            return new[]
            {
                WebExceptionStatus.ConnectionClosed,
                WebExceptionStatus.Timeout,
                WebExceptionStatus.RequestCanceled,
                WebExceptionStatus.ReceiveFailure
            }.Contains(webException.Status);
        }
    }
}