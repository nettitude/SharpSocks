using Microsoft.AspNetCore.Http;

namespace SharpSocksServer.HttpServer
{
    public interface IProcessRequest
    {
        void ProcessRequest(HttpContext httpContext);
    }
}