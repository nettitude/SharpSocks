using System;
using System.Net;

namespace SocksProxy.Classes.Extensions
{
    public class WebClientEx : WebClient
    {
        readonly string DEFAULTUSERAGENT = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.78 Safari/537.36";
        bool _sslFullValidation;
        String frontingDomain;
        public bool AutoRedirect { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public long ElapsedTime { get; set; }
        string _userAgent = null;
        public string UserAgent {
            get {
                if (String.IsNullOrWhiteSpace(_userAgent))
                    return DEFAULTUSERAGENT;
                else
                    return _userAgent;
            }
            set { _userAgent = value; }
        }

        public WebClientEx(CookieContainer container)
        {
            this.container = container;
            AutoRedirect = true;
        }

        public WebClientEx(CookieContainer container, bool SSLFullValidation = true)
        {
            this.container = container;
            _sslFullValidation = SSLFullValidation;
            AutoRedirect = true;
        }

        public WebClientEx(CookieContainer container, String FrontingDomain, bool SSLFullValidation = true)
        {
            this.container = container;
            _sslFullValidation = SSLFullValidation;
            AutoRedirect = true;
            frontingDomain = FrontingDomain;
        }

        public WebClientEx(bool SSLFullValidation)
        {
            _sslFullValidation = SSLFullValidation;
            AutoRedirect = true;
        }

        public CookieContainer CookieContainer
        {
            get { return container; }
            set { container = value; }
        }

        private CookieContainer container = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);

            ((HttpWebRequest)r).AllowAutoRedirect = AutoRedirect;
            ((HttpWebRequest)r).ServicePoint.Expect100Continue = false;
            
            ((HttpWebRequest)r).UserAgent = UserAgent;

            var request = r as HttpWebRequest;
            if (!_sslFullValidation)
                ServicePointManager.ServerCertificateValidationCallback = (z, y, x, w) => { return true; };

            if (request != null)
                request.CookieContainer = container;
            return r;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {

            var response = (HttpWebResponse)base.GetWebResponse(request, result);
            ReadCookies(response);
            StatusCode = response.StatusCode;
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = (HttpWebResponse)base.GetWebResponse(request);
            ReadCookies(response);
            StatusCode = response.StatusCode;
            return response;
        }

        private void ReadCookies(WebResponse r)
        {
            if (r is HttpWebResponse response)
            {
                CookieCollection cookies = response.Cookies;
                container.Add(cookies);
            }
        }
    }
}
