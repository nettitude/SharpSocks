using SharpSocksImplantTestApp.ImplantComms;
using NDesk.Options;
using System.Collections.Generic;
using System;
using System.Net;
using SharpSocksImplantTestApp.Helper;
using System.Security;
using SocksProxy.Classes.Integration;

namespace SharpSocksImplantTestApp
{
    public class Program
    {
        static LogToConsole comms = new LogToConsole();
        static void Main(string[] args)
        {
            bool help = false;
            String serverUri = null;
            String commandChannelId = null;
            String domain = null;
            String username = null;
            String password = null;
            String proxyUrl = null;
            String payloadCookieName = null;
            String sessionCookieName = null;
            String userAgent = null;
            String dfHost = null;
            String key = null;
            short beaconTime = 0;
            bool useProxy = false;
            bool userDefinedProxy = false;
            var errors = new List<String>();
            var warnings = new List<String>();

            var p = new OptionSet() {
                { "use-proxy","Use proxy server (for system proxy set this and leave -m blank)" , v => useProxy = v != null },
                { "m=|proxy=", "Proxy Url in format http://<server>:<port> (use-proxy is implied)", v => proxyUrl = v},
                { "u=|username=", "Web proxy username ", v => username = v},
                { "d=|domain=", "Web proxy domain ", v => domain = v},
                { "p=|password=", "Web proxy password ", v => password = v},
                { "k=|encryption-key=", "The encryption key, leave blank to be asked", v => key = v},
                { "c=|cmd-id=", "Command Channel Id (required) ", v => commandChannelId = v},
                { "b=|beacon=", "Beacon time in (ms)", v => beaconTime = short.Parse(v)},
                { "s=|server-uri=","Uri of the server, default is http://127.0.0.1:8081" , v => serverUri = v },
                { "session-cookie=","The name of the cookie to pass the session identifier" , v => sessionCookieName = v },
                { "payload-cookie=","The name of the cookie to pass smaller requests through" , v => payloadCookieName = v },
                { "user-agent=","The User Agent to be sent in any web request" , v => userAgent = v },
                { "df=","The actual Host header to be sent if using domain fronting" , v => dfHost = v },
                { "h|?|help",   v => help = v != null },
            };
            var extra = p.Parse(args);

            var defaultCmdChannel = "7f404221-9f30-470b-b05d-e1a922be3ff6";
            if (String.IsNullOrWhiteSpace(commandChannelId))
            {
                warnings.Add($"Command Channel Id is blank defaulting to {defaultCmdChannel}");
                if (String.IsNullOrWhiteSpace(commandChannelId))
                    commandChannelId = defaultCmdChannel;
            }

            if (String.IsNullOrWhiteSpace(serverUri))
            {
                warnings.Add(@"Server's URI is blank defaulting to http://127.0.0.1:8081");
                if (String.IsNullOrWhiteSpace(serverUri))
                    serverUri = "http://127.0.0.1:8081";
            }

            var result = Uri.TryCreate(serverUri, UriKind.Absolute, out Uri parsedServerUri);
            if (!result)
                errors.Add($"Server URI {serverUri} is not valid");

            IWebProxy wbProxy = null;
            if(!String.IsNullOrWhiteSpace(proxyUrl))
            {
                result =  Uri.TryCreate(proxyUrl, UriKind.Absolute, out Uri proxyUri);
                if (!result)
                    errors.Add($"Proxy URI {proxyUri} is not valid");
                else
                {
                    if (!String.IsNullOrWhiteSpace(username))
                    {
                        SecureString secPassword = null;
                        if (String.IsNullOrWhiteSpace(password))
                        {
                            Console.WriteLine("Please enter your proxy password: ");
                            secPassword = ConsolePassword.ReadPasswordFromConsole();
                        }
                        else
                        {
                            secPassword = new SecureString();
                            foreach (var n in password.ToCharArray())
                            {
                                secPassword.AppendChar(n);
                            }
                        }

                        ICredentials cred = null;
                        if (!String.IsNullOrWhiteSpace(domain))
                            cred = new NetworkCredential(username, secPassword, domain);
                        else
                            cred = new NetworkCredential(username, secPassword);

                        wbProxy = new WebProxy(proxyUri, false, new List<String>().ToArray(), cred);

                    }
                    else
                        wbProxy = new WebProxy(proxyUri, false, new List<String>().ToArray());

                    userDefinedProxy = useProxy = true;
                }
            }

            Banner();
            if (help)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }
            else if (errors.Count > 0)
            {
                errors.ForEach(x => { comms.LogError(x); });
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            SecureString secKey = null;
            if (String.IsNullOrWhiteSpace(key))
                secKey = ConsolePassword.ReadPasswordFromConsole();
            else
            {
                secKey = new SecureString();
                foreach (var n in key) secKey.AppendChar(n);
            }
            
            var sock = PoshCreateProxy.CreateSocksController(parsedServerUri, 
                                                            commandChannelId, 
                                                            dfHost, 
                                                            userAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.78 Safari/537.36", 
                                                            secKey, 
                                                            new List<String> {"Upload" }, 
                                                            sessionCookieName ?? "ASP.NET_SessionId", payloadCookieName ?? "__RequestVerificationToken", 
                                                            (useProxy) ? ((userDefinedProxy) ? wbProxy : System.Net.HttpWebRequest.GetSystemWebProxy()) : null, 
                                                            5000, 
                                                            null);
            
            Console.WriteLine("Ready to start cmd loop?");
            Console.ReadLine();
            sock.Start();
            Console.WriteLine("Hit [x] to quit");
            var str = "";
            while ("x" != (str = Console.ReadLine()))
            {
            }
        }

        static void Banner()
        {
            comms.BannerMesg("SOCKS PROXY Implant Test Client" +
            "\r\nv0.1" +
            "\r\nby Rob Maslen (2017)\r\n=================\r\n");
        }
    }
}
