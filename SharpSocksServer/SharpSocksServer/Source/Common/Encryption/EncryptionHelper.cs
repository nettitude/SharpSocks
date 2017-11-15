using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Classes.Encryption
{
    public class ECDHEncryptionHelper : IEncryptionHelper
    {
        System.Security.Cryptography.ECDiffieHellmanCng _ecDiffie;
        List<byte> _key = new List<byte>();

        public String Initialize()
        {
            return BuildECDH();
        }
        
        public String TheirPublicKey
        {
            set
            {
                try
                {
                    DeriveKey(value);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to derive server key " + ex.Message);
                }
            }
        }

        String BuildECDH()
        {
            _ecDiffie = new System.Security.Cryptography.ECDiffieHellmanCng()
            {
                KeyDerivationFunction = System.Security.Cryptography.ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = System.Security.Cryptography.CngAlgorithm.Sha256
            };
            return System.Convert.ToBase64String(_ecDiffie.PublicKey.ToByteArray());
        }

        void DeriveKey(String publicKeyb64)
        {
            List<byte> serverKey = System.Convert.FromBase64String(publicKeyb64).ToList();

            var eckey = System.Security.Cryptography.ECDiffieHellmanCngPublicKey.FromByteArray(serverKey.ToArray(), System.Security.Cryptography.CngKeyBlobFormat.EccPublicBlob);
            var key = _ecDiffie.DeriveKeyMaterial(eckey);
            System.Security.Cryptography.ProtectedMemory.Protect(key, System.Security.Cryptography.MemoryProtectionScope.SameProcess);
            _key.AddRange(key);
            Array.Clear(key, 0, key.Length);
        }

        public String Encrypt(List<byte> payload)
        {
            if (_key.Count() == 0)
                throw new Exception("Key hasn't been derived yet, encryption isn't available");

            string encPayload = null;
            byte[] key = null;
            try
            {
                var result = new List<byte>();
                using (var aes = new System.Security.Cryptography.AesManaged())
                {
                    aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                    aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                    aes.GenerateIV();
                    result.AddRange(aes.IV);
                    key = _key.ToArray();
                    System.Security.Cryptography.ProtectedMemory.Unprotect(key, System.Security.Cryptography.MemoryProtectionScope.SameProcess);
                    aes.Key = key;
                    var enc = aes.CreateEncryptor();
                    result.AddRange(enc.TransformFinalBlock(payload.ToArray(), 0, payload.Count));
                    encPayload = System.Convert.ToBase64String(result.ToArray());
                    result.Clear();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Encryption failed " + ex.Message);
            }
            finally
            {
                if (key != null)
                    Array.Clear(key, 0, key.Length);
            }
            return encPayload;
        }

        public List<byte> Decrypt(string encodedEncPayload)
        {
            if (_key.Count() == 0)
                throw new Exception("Key hasn't been derived yet, encryption isn't available");

            var result = new List<byte>();
            var encPayloadIV = Convert.FromBase64String(encodedEncPayload);
            var IV = encPayloadIV.Take(16);
            var payload = encPayloadIV.Skip(16).ToList();
            byte[] key = null;

            using (var aes = new System.Security.Cryptography.AesManaged())
            {
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                key = _key.ToArray();
                System.Security.Cryptography.ProtectedMemory.Unprotect(key, System.Security.Cryptography.MemoryProtectionScope.SameProcess);
                aes.Key = key;
                aes.IV = IV.ToArray();
                var enc = aes.CreateDecryptor();
                return enc.TransformFinalBlock(payload.ToArray(), 0, payload.Count()).ToList();
            }
        }
    }
}
