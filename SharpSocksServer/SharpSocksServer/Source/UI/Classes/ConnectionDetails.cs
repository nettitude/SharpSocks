using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpSocksServer.Source.UI.Classes
{
    public class ConnectionDetails
    {
        public String Id { get; set; }
        public String HostPort { get; set; }
        public String Status { get; set; }
        public Int32 DataSent { get; set; }
        public Int32 DataRecv { get; set; }
        public String UpdateTime { get; set; }
    }
}
