using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImplantSide.Interfaces
{
    public interface ITamper
    {
        String TamperPayload(String payload);
        String TamperUri(Uri host, String payload);
    }
}
