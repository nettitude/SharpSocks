using Common.Classes.Encryption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace Common.Encryption.Debug
{

	/// <summary>
	/// This is a simple encryptor 
	/// </summary>
	public class DebugSimpleEncryptor : IEncryptionHelper
	{
		List<byte> _key = new List<byte>();

		public DebugSimpleEncryptor(String base64Key)
		{
			_key.AddRange(Convert.FromBase64String(base64Key));
		}

		public List<byte> Decrypt(string encodedEncPayload)
		{
			var ciphrBytes = Convert.FromBase64String(encodedEncPayload).ToList();
			using (var aes = new System.Security.Cryptography.RijndaelManaged())
			{
				aes.Mode = System.Security.Cryptography.CipherMode.CBC;
				aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
				aes.IV = ciphrBytes.Take(16).ToArray();
				aes.Key = _key.ToArray();
				var dec = aes.CreateDecryptor();
				var encBytes = ciphrBytes.Skip(16).ToArray();
				var plainbytes = dec.TransformFinalBlock(encBytes, 0, encBytes.Length).ToList();
				return plainbytes;
			}
		}

		public string Encrypt(List<byte> payload)
		{
			using (var aes = new System.Security.Cryptography.RijndaelManaged())
			{
				var result = new List<byte>();
				aes.Mode = System.Security.Cryptography.CipherMode.CBC;
				aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
				aes.GenerateIV();
				aes.Key = _key.ToArray();
				var enc = aes.CreateEncryptor();
				result.AddRange(aes.IV);
				result.AddRange(enc.TransformFinalBlock(payload.ToArray(), 0, payload.Count));
				return System.Convert.ToBase64String(result.ToArray());
			}
		}

		public string Initialize()
		{
			//NO WORK TO DO IN THIS VERSION
			return "USING DEBUG SIMPLE ENCRYPTOR";
		}
	}
}
