using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace SharpSocksCommon.Encryption
{
    public class RijndaelCBCCryptor : IEncryptionHelper
    {
        private readonly List<byte> _key = new List<byte>();

        public RijndaelCBCCryptor(string base64Key)
        {
            Console.WriteLine("[*] Using Rijndael CBC encryption");
            _key.AddRange(Convert.FromBase64String(base64Key));
        }

        public List<byte> Decrypt(string encodedEncPayload)
        {
            var list = Convert.FromBase64String(encodedEncPayload).ToList();
            using (var rijndaelManaged = new RijndaelManaged())
            {
                rijndaelManaged.Mode = CipherMode.CBC;
                rijndaelManaged.Padding = PaddingMode.PKCS7;
                rijndaelManaged.IV = list.Take(16).ToArray();
                rijndaelManaged.Key = _key.ToArray();
                var decryptor = rijndaelManaged.CreateDecryptor();
                var array = list.Skip(16).ToArray();
                var length = array.Length;
                return decryptor.TransformFinalBlock(array, 0, length).ToList();
            }
        }

        public string Encrypt(List<byte> payload)
        {
            using (var rijndaelManaged = new RijndaelManaged())
            {
                var byteList = new List<byte>();
                rijndaelManaged.Mode = CipherMode.CBC;
                rijndaelManaged.Padding = PaddingMode.PKCS7;
                rijndaelManaged.GenerateIV();
                rijndaelManaged.Key = _key.ToArray();
                var encryptor = rijndaelManaged.CreateEncryptor();
                byteList.AddRange(rijndaelManaged.IV);
                byteList.AddRange(encryptor.TransformFinalBlock(payload.ToArray(), 0, payload.Count));
                return Convert.ToBase64String(byteList.ToArray());
            }
        }

        public string Initialize()
        {
            return null;
        }
    }
}