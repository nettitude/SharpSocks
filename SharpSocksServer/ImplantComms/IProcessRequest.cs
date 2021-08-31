using System.Net;

namespace SharpSocksServer.ImplantComms
{
    public interface IProcessRequest
    {
        void ProcessRequest(HttpListenerContext httpListenerContext);
    }
}