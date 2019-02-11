using System;
using System.Linq;


namespace ImplantSide.Classes.Helpers
{
    public static class WebExceptionAnalyzer
    {
        //This checks to see if it is worth attempting to recover from the exception that has been thrown by resending
        public static bool IsTransient(Exception ex)
        {
            var webException = ex as System.Net.WebException;
            if (webException != null)
            {
                // If the web exception contains one of the following status values 
                // it may be transient.
                if (new[] {System.Net.WebExceptionStatus.ConnectionClosed,
                        System.Net.WebExceptionStatus.Timeout,
                        System.Net.WebExceptionStatus.RequestCanceled,
                        System.Net.WebExceptionStatus.ReceiveFailure,
                        }.Contains(webException.Status))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
