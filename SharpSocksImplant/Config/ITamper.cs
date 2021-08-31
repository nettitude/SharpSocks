using System;

namespace SharpSocksImplant.Config
{
    public interface ITamper
    {
        string TamperPayload(string payload);

        string TamperUri(Uri host, string payload);
    }
}