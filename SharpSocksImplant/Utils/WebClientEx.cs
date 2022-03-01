using System;
using System.Net;

namespace SharpSocksImplant.Utils
{
    public class WebClientEx : WebClient
    {
        private const string DEFAULT_USERAGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.78 Safari/537.36";
        private readonly bool _insecureSSL;
        private string _userAgent;

        public WebClientEx(CookieContainer container)
        {
            CookieContainer = container;
            AutoRedirect = true;
        }

        public WebClientEx(CookieContainer container, bool insecureSSL = true)
        {
            CookieContainer = container;
            _insecureSSL = insecureSSL;
            AutoRedirect = true;
        }


        public WebClientEx(bool insecureSSL)
        {
            _insecureSSL = insecureSSL;
            AutoRedirect = true;
        }

        public bool AutoRedirect { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public long ElapsedTime { get; set; }

        public string UserAgent
        {
            get => string.IsNullOrWhiteSpace(_userAgent) ? DEFAULT_USERAGENT : _userAgent;
            set => _userAgent = value;
        }

        public CookieContainer CookieContainer { get; set; } = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            var webRequest = base.GetWebRequest(address);
            ((HttpWebRequest)webRequest).AllowAutoRedirect = AutoRedirect;
            ((HttpWebRequest)webRequest).ServicePoint.Expect100Continue = false;
            ((HttpWebRequest)webRequest).UserAgent = UserAgent;
            var httpWebRequest = webRequest as HttpWebRequest;
            if (_insecureSSL)
                ServicePointManager.ServerCertificateValidationCallback = (z, y, x, w) => true;
            if (httpWebRequest != null)
                httpWebRequest.CookieContainer = CookieContainer;
            return webRequest;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            var webResponse = (HttpWebResponse)base.GetWebResponse(request, result);
            ReadCookies(webResponse);
            StatusCode = webResponse.StatusCode;
            return webResponse;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var webResponse = (HttpWebResponse)base.GetWebResponse(request);
            ReadCookies(webResponse);
            StatusCode = webResponse.StatusCode;
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