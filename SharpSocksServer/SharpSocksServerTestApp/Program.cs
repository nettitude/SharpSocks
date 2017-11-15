using NDesk.Options;
using SharpSocksServer.ServerComms;
using System;
using System.Collections.Generic;
using SharpSocksServer.SharpSocksServer.Classes;
using SocksTunnel.Classes;
using SocksServer.Classes.Server;
using Common.Encryption.Debug;
using SharpSocksServer.Source.Integration;

namespace SharpSocksServer
{
    public class Program
    {
        static DebugConsoleOutput debugComms = new DebugConsoleOutput();
        static void Main(string[] args)
        {
            bool help = false;
            bool verbose = false;
            String serverUri = null;
            String socksServerUri = null;
            String commandChannelId = null;
            String EncryptionKey = null;
            String payloadCookieName = null;
            String sessionCookieName = null;
            String socksIpToListen = "*";
            ushort socksPort = 0;
            var errors = new List<String>();
            var warnings = new List<String>();

            var p = new OptionSet() {
                { "c=|cmd-id=", "Command Channel Identifier, needs to be shared with the server", v => commandChannelId = v},
                { "l=|http-server-uri=","Uri to listen on, default is http://127.0.0.1:8081" , v => serverUri = v },
                { "s=|socks-server-uri=","IP:Port for socks to listen on, default is *:43334" , v => socksServerUri = v },
                { "k=|encryption-key=","The encryption key used to secure comms" , v => EncryptionKey = v },
                { "sc=|session-cookie=","The name of the cookie to pass the session identifier" , v => sessionCookieName = v },
                { "pc=|payload-cookie=","The name of the cookie to pass smaller requests through" , v => payloadCookieName = v },
                { "v|verbose", "Verbose error logging",   v => verbose = v != null },
                { "h|?|help",   v => help = v != null },
            };
            var extra = p.Parse(args);

            if (verbose)
                debugComms.SetVerboseOn();

            if (String.IsNullOrWhiteSpace(serverUri))
            {
                warnings.Add(@"Uri to listen is blank defaulting to http://127.0.0.1:8081");
                if (String.IsNullOrWhiteSpace(serverUri))
                    serverUri = "http://127.0.0.1:8081";
            }

            var defaultCmdChannel = "7f404221-9f30-470b-b05d-e1a922be3ff6";

            if (String.IsNullOrWhiteSpace(commandChannelId))
            {
                warnings.Add($"Command Channel Id is blank defaulting to {defaultCmdChannel}");
                if (String.IsNullOrWhiteSpace(commandChannelId))
                    commandChannelId = defaultCmdChannel;
            }

            var result = Uri.TryCreate(serverUri, UriKind.Absolute, out Uri parsedServerUri);
            if (!result)
                errors.Add($"uri to listen on {serverUri} is not valid");

            if (String.IsNullOrWhiteSpace(EncryptionKey))
                errors.Add($"Encryption key is null or blank");

            if (String.IsNullOrWhiteSpace(socksServerUri))
                socksServerUri = "*:43334";

            if (!socksServerUri.Contains(":"))
                errors.Add($"Socks IP not in {socksServerUri} IP:port format");
            else
            {
                var spltIpPort = socksServerUri.Split(':');
                if(spltIpPort.Length > 1)
                    socksIpToListen = spltIpPort[0];

                if (!ushort.TryParse(spltIpPort[1], out socksPort))
                    errors.Add($"The SOCKS port is not a number");
            }

            Banner();
            Console.WriteLine("");
            if (help)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }
            else if (errors.Count > 0)
            {
                errors.ForEach(x => { debugComms.LogError(x); });
                Console.WriteLine("");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine("[x] to quit\r\n");

            PSSocksServer.CreateSocksController(socksIpToListen, serverUri, commandChannelId, socksPort, EncryptionKey, sessionCookieName, payloadCookieName, debugComms);

            var str = "";
            while( "x" != (str = Console.ReadLine()))
            {
            }
        }

        static void Banner()
        {
            debugComms.BannerMesg("SOCKS PROXY Test Server" +
            "\r\nv0.1" +
            "\r\nby Rob Maslen (2017)\r\n=================");
        }


    }
}
