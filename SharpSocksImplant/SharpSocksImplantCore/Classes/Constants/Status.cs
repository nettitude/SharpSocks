using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImplantSide.Classes.Constants
{
    public enum STATUS
    {
        ERROR,
        CREATED,
        IDLE,
        CONFIRMING,
        CONFIRMED,
        PROXYOPEN,
        SENDING,
        RECEIVING,
        CLOSED,
        EXCHANGING,
        EXCHANGED
    }
}
