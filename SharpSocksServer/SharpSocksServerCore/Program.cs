using McMaster.Extensions.CommandLineUtils;
using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using SharpSocksServer.ServerComms;
using SharpSocksServer.Source.Integration;

namespace SharpSocksCore
{
	class Program
	{
		static readonly StringBuilder _log = new StringBuilder();
		static readonly List<String> _warnings = new List<String>();
		static readonly List<String> _errors = new List<String>();
		static DebugConsoleOutput _debugComms = new DebugConsoleOutput();
		static CommandLineApplication _app = null;
		static void Main(string[] args)
		{
			var errors = new List<String>();
			var app = new CommandLineApplication();
			_app = app;
			_app.HelpOption();

			var optSocksServerUri = _app.Option("-s|--socksserveruri", "IP:Port for SOCKS to listen on, default is *:43334", CommandOptionType.SingleValue);
			var optCmdChannelId = _app.Option("-c|--cmdid", "Command Channel Identifier, needs to be shared with the server", CommandOptionType.SingleValue);
			var optHttpServer = _app.Option("-l|--httpserveruri", "Uri to listen on, default is http://127.0.0.1:8081", CommandOptionType.SingleValue);
			var optEncKey = _app.Option("-k|--encryptionkey", "The encryption key used to secure comms", CommandOptionType.SingleValue);
			var optSessionCookie = _app.Option("-sc|--sessioncookie", "The name of the cookie to pass the session identifier", CommandOptionType.SingleValue);
			var optPayloadCookie = _app.Option("-pc|--payloadcookie", "The name of the cookie to pass smaller requests through", CommandOptionType.SingleValue);
			var optSocketTimeout = _app.Option("-st|--socketTimeout", "How long should SOCKS sockets be held open for, default is 120s", CommandOptionType.SingleValue);
			var optVerbose = _app.Option("-v|--verbose", "Verbose error logging", CommandOptionType.NoValue);
			//var optHelp = _app.Option("-h|-?|-help", "Help", CommandOptionType.NoValue);

			_app.OnExecute(() =>
			{
				string errPort = "<blank>";

				String socksServerUri = null;
				String socksIpToListen = null;
				if (!optSocksServerUri.HasValue() || String.IsNullOrWhiteSpace(optSocksServerUri.Value()))
					socksServerUri = "*:43334";
				else
					socksServerUri = optSocksServerUri.Value();
				ushort socksPort = 0;
				if (!socksServerUri.Contains(":"))
					errors.Add($"Socks IP not in {socksServerUri} IP:port format");
				else
				{
					var spltIpPort = socksServerUri.Split(':');
					if (spltIpPort.Length > 1)
						socksIpToListen = spltIpPort[0];

					if (!ushort.TryParse(spltIpPort[1], out socksPort) && socksPort < 1024)
					{
						errors.Add($"[!] Port [{errPort}] is not valid (or is less than 1024)");
						_debugComms.LogMessage(_app.GetHelpText());
						return;
					}
				}

				UInt32 socketTimeout = 120;
				var sockvalid = false;
				if (optSocketTimeout.HasValue() && (UInt32.TryParse(optSocketTimeout.Value(), out socketTimeout)))
					sockvalid = true;
				
				if (!sockvalid)
				{
					socketTimeout = 120;
					_warnings.Add($@"Defaulting Socket Timeout to {socketTimeout}s");
				}
				socketTimeout *= 1000; //Convert seconds to milliseconds

				if (optVerbose.HasValue())
					_debugComms.SetVerboseOn();

				StartSocks(socksIpToListen, ValidateServerUri(optHttpServer.Value()), ValidateCmdChannelId(optCmdChannelId.Value()),
					socksPort, ValidateEncryptionKey(optEncKey.Value()), optSessionCookie.Value(), optPayloadCookie.Value(), socketTimeout, false);
			});
			try
			{
				_app.Execute(args);
			}
			catch
			{
				Console.WriteLine(_app.GetHelpText());
			}
		}

		static String ValidateServerUri(string serverUri)
		{
			String output = null;
			if (String.IsNullOrWhiteSpace(serverUri))
			{
				_warnings.Add(@"Uri to listen is blank defaulting to http://127.0.0.1:8081");
				if (String.IsNullOrWhiteSpace(serverUri))
					output = "http://127.0.0.1:8081";
			}
			else
				output = serverUri;
			return output;
		}

		static String ValidateCmdChannelId(String commandChannelId)
		{
			var defaultCmdChannel = "7f404221-9f30-470b-b05d-e1a922be3ff6";
			if (String.IsNullOrWhiteSpace(commandChannelId))
			{
				_warnings.Add($"Command Channel Id is blank defaulting to {defaultCmdChannel}");
				if (String.IsNullOrWhiteSpace(commandChannelId))
					return defaultCmdChannel;
			}
			return commandChannelId;
		}

		static String ValidateEncryptionKey(String EncryptionKey)
		{
			String newKey = null;
			if (String.IsNullOrWhiteSpace(EncryptionKey))
			{
				var aes = AesManaged.Create();
				aes.GenerateKey();
				newKey = System.Convert.ToBase64String(aes.Key);
				_warnings.Add($"Using encryption key {newKey}");
				return newKey;
			}
			return EncryptionKey;
		}

		static void StartSocks(String socksIpToListen, String serverUri, String commandChannelId, UInt16 socksPort, String EncryptionKey, String sessionCookieName, String payloadCookieName, UInt32 SocketTimeout, bool help = false)
		{
			Banner();
			Console.WriteLine("");
			if (help)
			{
				Console.WriteLine(_app.GetHelpText());
				return;
			}
			else if (_errors.Count > 0)
			{
				_errors.ForEach(x => { _debugComms.LogError(x); });
				Console.WriteLine("");
				Console.WriteLine(_app.GetHelpText());
				return;
			}
			if (_warnings.Count > 0)
			{
				_warnings.ForEach(x => { _debugComms.LogMessage(x); });
				Console.WriteLine("");
			}

			Console.WriteLine("[x] to quit\r\n");

			PSSocksServer.CreateSocksController(socksIpToListen, serverUri, null, commandChannelId, socksPort, EncryptionKey, sessionCookieName, payloadCookieName, _debugComms, SocketTimeout);
			var str = "";
			while ("x" != (str = Console.ReadLine()))
			{
				if (str.StartsWith("LPoll="))
				{
					var splt = str.Split('=');
					if (splt.Length > 1)
						if (Int32.TryParse(splt[1], out int result))
						{
							PSSocksServer.SetLongPollTimeout(result);
							continue;
						}
					Console.WriteLine("[X] New Long Poll format ");
				}
			}
		}

		static void Banner()
		{
			_debugComms.BannerMesg("SharpsSOCKS .net core" +
			"\r\nv0.1" +
			"\r\nby Rob Maslen (2019)\r\n=================");
		}
	}
}
