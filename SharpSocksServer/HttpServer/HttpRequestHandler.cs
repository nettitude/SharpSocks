using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SharpSocksServer.Logging;

namespace SharpSocksServer.HttpServer
{
    public class HttpRequestHandler
    {
        private readonly ILogOutput _logOutput;
        private readonly IProcessRequest _processRequest;

        public HttpRequestHandler(IProcessRequest processRequest, ILogOutput logOutput)
        {
            _processRequest = processRequest;
            _logOutput = logOutput;
        }

        public Task HandleRequest(HttpContext context)
        {
            _logOutput.LogMessage($"Handling request from {context.Request.Host}");
            return Task.Run(() => _processRequest.ProcessRequest(context));
        }
    }
}