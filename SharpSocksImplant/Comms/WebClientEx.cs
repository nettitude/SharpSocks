using System;
using System.Net;

namespace SharpSocksImplant.Comms
{
    public class WebClientEx : WebClient
    {
        private const string DEFAULT_USERAGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.78 Safari/537.36";

        private readonly bool _insecureSSL;
        private string _userAgent;

        public WebClientEx(CookieContainer container, bool insecureSSL = true)
        {
            CookieContainer = container;
            _insecureSSL = insecureSSL;
            AutoRedirect = true;
        }

        private bool AutoRedirect { get; }

        public string UserAgent
        {
            get => string.IsNullOrWhiteSpace(_userAgent) ? DEFAULT_USERAGENT : _userAgent;
            set => _userAgent = value;
        }

        private CookieContainer CookieContainer { get; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var webRequest = (HttpWebRequest)base.GetWebRequest(address);
            if (webRequest == null)
            {
                return null;
            }

            webRequest.AllowAutoRedirect = AutoRedirect;
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.UserAgent = UserAgent;
            if (_insecureSSL)
                ServicePointManager.ServerCertificateValidationCallback = (z, y, x, w) => true;
            webRequest.CookieContainer = CookieContainer;
            return webRequest;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            var webResponse = (HttpWebResponse)base.GetWebResponse(request, result);
            ReadCookies(webResponse);
            return webResponse;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var webResponse = (HttpWebResponse)base.GetWebResponse(request);
            ReadCookies(webResponse);
            return webResponse;
        }

        private void ReadCookies(WebResponse response)
        {
            if (!(response is HttpWebResponse httpWebResponse))
                return;
            CookieContainer.Add(httpWebResponse.Cookies);
        }
    }
}