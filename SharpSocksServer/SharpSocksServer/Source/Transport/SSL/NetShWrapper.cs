using Common.Server.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SharpSocksServer.Source.Transport.SSL
{
    public class NetshWrapper
    {
        public ILogOutput ServerComms { get; set; }
        const string ADDCERTERROR = "SSL Certificate add failed, Error:";
        const string ADDCERTSUCCESS = "SSL Certificate successfully added";
        const string ADDCERTPARAMINVALID = "One or more essential parameters were not entered.";
        const string CERTFILENOTFOUNDERROR = "The system cannot find the file specified.";
        const string PARAMISINCORRECTERROR = "The parameter is incorrect.";

        public bool CheckIfCertIsInCAStore(X509Certificate2 x509cert)
        {
            X509Store store = null;
            try
            {
                store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    if (Enumerable.SequenceEqual(x509cert.GetCertHash(), cert.GetCertHash()))
                        return true;
                }
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to enumerate CA Store, error {ex.Message}");
            }
            finally
            {
                if (null != store)
                    store.Close();
            }

            return false;
        }


        public bool ImportCertIntoCAStore(X509Certificate2 x509cert)
        {
            X509Store store = null;
            try
            {
                store = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Add(x509cert);
                return true;
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to add cert from CA Store, error {ex.Message}");
                return false;
            }
            finally
            {
                if (null != store)
                    store.Close();
            }
        }

        public bool RemoveCertFromCAStore(X509Certificate2 x509cert)
        {
            System.Security.Cryptography.X509Certificates.X509Store store = null;
            try
            {
                store = new X509Store(StoreName.CertificateAuthority,StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Remove(x509cert);
                return true;
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to remove cert from CA Store, error {ex.Message}");
                return false;
            }
            finally
            {
                if (null != store)
                    store.Close();
            }
        }


        public  bool CheckIfCertBoundToPort(String host, String port, ref Dictionary<String, String> certDetails)
        {
            bool result = false;
            var innerCertDetails = certDetails;
            try
            {
                string certConfig = $@"http show sslcert ipport={host}:{port}";
                var netshOutpt = RunNetShCmd(certConfig);
                if (!netshOutpt.Contains(CERTFILENOTFOUNDERROR))
                {
                    result = true;
                    if (null != innerCertDetails)
                    {
                        System.Text.RegularExpressions.Regex.Split(netshOutpt, "\r\n|\r|\n").ToList().ForEach(x =>
                        {
                            if (x.StartsWith(new String(' ', 4)))
                            {
                                var mch = x.TrimStart().Split(new string[] { " :" }, StringSplitOptions.RemoveEmptyEntries);
                                if (2 == mch.Length)
                                    innerCertDetails.Add(mch[0].Trim(), mch[1].Trim());
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Exception has fired : {ex.Message}");
                return false;
            }
            return result;
        }

        public  string GetCertHashString(String pfxFile, System.Security.SecureString password)
        {
            System.Security.Cryptography.X509Certificates.X509Certificate2 pfx = null;
            try
            {
                pfx = new X509Certificate2(pfxFile, password);
                var hsh = pfx.GetCertHashString();
                return hsh;
            }

            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to get the CertHash, Error : {ex.Message}");
                return null;
            }
        }

        public bool RemoveCertFromIPPort(string host, string port)
        {
            try
            {
                var deleteNetShcmd = $"http delete sslcert ipport={host}:{port}";
                var netshOutpt = RunNetShCmd(deleteNetShcmd);
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to remove the Cert from ipport={host}:{port} Error: {ex.Message}");
                return false;
            }
            return true;
        }

        public bool AddCertificateFromPfxToIPPort(String host, String port, String pfxFile, System.Security.SecureString password)
        {
            if (!File.Exists(pfxFile))
                throw new Exception($"File {pfxFile} does not exist");

            System.Security.Cryptography.X509Certificates.X509Certificate2 pfx = null;
            try
            {
                pfx = new System.Security.Cryptography.X509Certificates.X509Certificate2(pfxFile, password);
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to add the cert {pfxFile} to ipport={host}:{port} Error: {ex.Message}");
                return false;
            }
            return AddCertificateToIPPort(host, port, pfx.GetCertHashString());
        }

        public bool AddCertificateToIPPort(String host, String port, String certHash)
        {
            String netshOutpt = null;
            bool result = false;
            try
            {
                string addCertNetShCmd = $"http add sslcert ipport={host}:{port} certhash={certHash} appid={{00112233-4455-6677-8899-AABBCCDDEEFF}}";
                netshOutpt = RunNetShCmd(addCertNetShCmd);
            }
            catch (Exception ex)
            {
                ServerComms.LogError($@"Unable to add the cert {{{certHash}}} to ipport={host}:{port} Error: {ex.Message}");
                return false;
            }

            String strErrorCode = null;
            if (netshOutpt.Contains(ADDCERTERROR))
            {
                System.Text.RegularExpressions.Regex.Split(netshOutpt, "\r\n|\r|\n").ToList().ForEach(x =>
                {
                    if (x.Contains(ADDCERTERROR))
                    {
                        var idx = netshOutpt.IndexOf(ADDCERTERROR) + ADDCERTERROR.Length;
                        strErrorCode = netshOutpt.Substring(idx).Trim();
                        throw new Exception($"Import of certificate has failed error code {strErrorCode}");
                    }
                });
            }
            else if (netshOutpt.Contains(ADDCERTPARAMINVALID))
            {
                throw new Exception("Import of certificate has failed, params are invalid");
            }
            else if (netshOutpt.Contains(ADDCERTSUCCESS))
            {
                result = true;
            }
            return result;
        }

        String RunNetShCmd(String netShCmd)
        {
            var netShProcStrt = new System.Diagnostics.ProcessStartInfo
            {
                RedirectStandardOutput = true
            };
            var sb = new StringBuilder("");

            // Set our event handler to asynchronously read the sort output.
            netShProcStrt.WorkingDirectory = @"C:\Windows\System32";
            netShProcStrt.FileName = @"C:\Windows\System32\netsh.exe";
            netShProcStrt.Verb = "runas";
            netShProcStrt.Arguments = netShCmd;
            netShProcStrt.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            netShProcStrt.UseShellExecute = false;
            netShProcStrt.CreateNoWindow = true;
            var proc = System.Diagnostics.Process.Start(netShProcStrt);

            return proc.StandardOutput.ReadToEnd();
        }
    }
}
