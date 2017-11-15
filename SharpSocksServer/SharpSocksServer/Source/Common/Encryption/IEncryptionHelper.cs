using System.Collections.Generic;

namespace Common.Classes.Encryption
{
    public interface IEncryptionHelper
    {
        List<byte> Decrypt(string encodedEncPayload);
        string Encrypt(List<byte> payload);
        string Initialize();
    }
}