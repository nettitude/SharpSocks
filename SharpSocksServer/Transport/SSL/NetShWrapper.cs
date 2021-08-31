using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using SharpSocksServer.Logging;

namespace SharpSocksServer.Transport.SSL
{
    public class NetshWrapper
    {
        private const string ADDCERTERROR = "SSL Certificate add failed, Error:";
        private const string ADDCERTSUCCESS = "SSL Certificate successfully added";
        private const string ADDCERTPARAMINVALID = "One or more essential parameters were not entered.";
        private const string CERTFILENOTFOUNDERROR = "The system cannot find the file specified.";
        private const string PARAMISINCORRECTERROR = "The parameter is incorrect.";

        public ILogOutput ServerComms { get; set; }

        public bool CheckIfCertIsInLMStore(X509Certificate2 x509cert)
        {
            X509Store x509Store = null;
            try
            {
                x509Store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                x509Store.Open(OpenFlags.ReadWrite);
                var enumerator = x509Store.Certificates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    if (x509cert.GetCertHash().SequenceEqual(current.GetCertHash()))
                        return true;
                }
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to enumerate CA Store, error " + ex.Message);
            }
            finally
            {
                x509Store?.Close();
            }

            return false;
        }

        public bool ImportCertIntoLMStore(X509Certificate2 x509cert)
        {
            X509Store x509Store = null;
            try
            {
                x509Store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                x509Store.Open(OpenFlags.ReadWrite);
                x509Store.Add(x509cert);
                return true;
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to add cert from CA Store, error " + ex.Message);
                return false;
            }
            finally
            {
                x509Store?.Close();
            }
        }

        public bool RemoveCertFromLMStore(X509Certificate2 x509cert)
        {
            X509Store x509Store = null;
            try
            {
                x509Store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                x509Store.Open(OpenFlags.ReadWrite);
                x509Store.Remove(x509cert);
                return true;
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to remove cert from CA Store, error " + ex.Message);
                return false;
            }
            finally
            {
                x509Store?.Close();
            }
        }

        public bool CheckIfCertBoundToPort(
            string host,
            string port,
            ref Dictionary<string, string> certDetails)
        {
            var flag = false;
            var innerCertDetails = certDetails;
            try
            {
                var input = RunNetShCmd("http show sslcert ipport=" + host + ":" + port);
                if (!input.Contains("The system cannot find the file specified."))
                {
                    flag = true;
                    if (innerCertDetails != null)
                        Regex.Split(input, "\r\n|\r|\n").ToList().ForEach((Action<string>)(x =>
                        {
                            if (!x.StartsWith(new string(' ', 4)))
                                return;
                            var strArray = x.TrimStart().Split(new string[1]
                            {
                                " :"
                            }, StringSplitOptions.RemoveEmptyEntries);
                            if (2 != strArray.Length)
                                return;
                            innerCertDetails.Add(strArray[0].Trim(), strArray[1].Trim());
                        }));
                }
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Exception has fired : " + ex.Message);
                return false;
            }

            return flag;
        }

        public string GetCertHashString(string pfxFile, SecureString password)
        {
            try
            {
                return new X509Certificate2(pfxFile, password).GetCertHashString();
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to get the CertHash, Error : " + ex.Message);
                return null;
            }
        }

        public bool RemoveCertFromIPPort(string host, string port)
        {
            try
            {
                RunNetShCmd("http delete sslcert ipport=" + host + ":" + port);
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to remove the Cert from ipport=" + host + ":" + port + " Error: " + ex.Message);
                return false;
            }

            return true;
        }

        public bool AddCertificateFromPfxToIPPort(
            string host,
            string port,
            string pfxFile,
            SecureString password)
        {
            if (!File.Exists(pfxFile))
                throw new Exception("File " + pfxFile + " does not exist");
            X509Certificate2 x509Certificate2;
            try
            {
                x509Certificate2 = new X509Certificate2(pfxFile, password);
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to add the cert " + pfxFile + " to ipport=" + host + ":" + port + " Error: " + ex.Message);
                return false;
            }

            return AddCertificateToIPPort(host, port, x509Certificate2.GetCertHashString());
        }

        public bool AddCertificateToIPPort(string host, string port, string certHash)
        {
            string netshOutpt = null;
            var flag = false;
            try
            {
                netshOutpt = RunNetShCmd("http add sslcert ipport=" + host + ":" + port + " certhash=" + certHash + " appid={00112233-4455-6677-8899-AABBCCDDEEFF}");
            }
            catch (Exception ex)
            {
                ServerComms.LogError("Unable to add the cert {" + certHash + "} to ipport=" + host + ":" + port + " Error: " + ex.Message);
                return false;
            }

            string strErrorCode = null;
            if (netshOutpt.Contains("SSL Certificate add failed, Error:"))
            {
                Regex.Split(netshOutpt, "\r\n|\r|\n").ToList().ForEach((Action<string>)(x =>
                {
                    if (x.Contains("SSL Certificate add failed, Error:"))
                    {
                        strErrorCode = netshOutpt.Substring(netshOutpt.IndexOf("SSL Certificate add failed, Error:") + "SSL Certificate add failed, Error:".Length).Trim();
                        throw new Exception("Import of certificate has failed error code " + strErrorCode);
                    }
                }));
            }
            else
            {
                if (netshOutpt.Contains("One or more essential parameters were not entered."))
                    throw new Exception("Import of certificate has failed, params are invalid");
                if (netshOutpt.Contains("SSL Certificate successfully added"))
                    flag = true;
            }

            return flag;
        }

        private string RunNetShCmd(string netShCmd)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.RedirectStandardOutput = true;
            new StringBuilder("");
            startInfo.WorkingDirectory = "C:\\Windows\\System32";
            startInfo.FileName = "C:\\Windows\\System32\\netsh.exe";
            startInfo.Verb = "runas";
            startInfo.Arguments = netShCmd;
            startInfo.WindowStyle = (ProcessWindowStyle)1;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            return Process.Start(startInfo).StandardOutput.ReadToEnd();
        }
    }
}