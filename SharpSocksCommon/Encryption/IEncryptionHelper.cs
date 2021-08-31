using System.Collections.Generic;

namespace SharpSocksCommon.Encryption
{
    public interface IEncryptionHelper
    {
        List<byte> Decrypt(string encodedEncPayload);

        string Encrypt(List<byte> payload);

        string Initialize();
    }
}