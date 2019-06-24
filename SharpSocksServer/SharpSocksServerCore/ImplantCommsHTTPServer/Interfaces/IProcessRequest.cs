using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocksTunnel.Interfaces
{
    public interface IProcessRequest
    {
        void ProcessRequest(System.Net.HttpListenerContext ctx);
    }
}
