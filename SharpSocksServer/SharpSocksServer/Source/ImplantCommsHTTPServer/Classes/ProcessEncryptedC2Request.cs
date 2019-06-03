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
		readonly object _locker = new Object();
		readonly object _commandLocker = new Object();
		readonly object _listenerLocker = new Object();
		readonly object _dataTasksLocker = new Object();
		ManualResetEvent _cmdTaskWaitEvent = new ManualResetEvent(false);
        Dictionary<String, Listener> _listeners = new Dictionary<String, Listener>();
        Queue<XElement> _commandTasks = new Queue<XElement>();
        Dictionary<String, DataTask> _dataTasks = new Dictionary<String, DataTask>();
        public ushort CommandLimit { get; set; }

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
                if (sessionCookie == null || String.IsNullOrEmpty(sessionCookie.Value))
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
            String response = null;
            List<byte> responseBytes = new List<byte>();
            String uploadedPayload = null;

            //TO DO: Payload is currently coming up as different content types

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
                    if (null != payloadCookie)
                    {
                        if (!String.IsNullOrWhiteSpace(payloadCookie.Value))
                        {
                            uploadedPayload = payloadCookie.Value;
                        }
                    }
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
					var ready = false;
					Int32 ctr = 0, cmdTasks = 0;

					ProcessCommandChannelTime();

					if (null == uploadedPayload || uploadedPayload.Count() == 0)
						ServerComms.LogMessage($"Command channel sending {_commandTasks.Count()} tasks ");
					else
					{
						mapSessionToConnectionDetails[_commandChannel].DataRecv += uploadedPayload.Count();
						if (_commandTasks.Count() > 0)
							ServerComms.LogMessage($"Command channel payload {uploadedPayload.Count()} bytes, sending {_commandTasks.Count()} tasks ");
						ProcessCommandChanelImplantMessage(this._encryption.Decrypt(uploadedPayload));
					}

					lock (_commandLocker) { cmdTasks = _commandTasks.Count(); }
					if (cmdTasks == 0) while (!(ready = _cmdTaskWaitEvent.WaitOne(1000)) && ctr++ < 40) ;
					
					lock (_commandLocker) { cmdTasks = _commandTasks.Count(); }
					if (cmdTasks > 0)
						response = new XElement("Response", new XElement("Tasks", PopQueueCommandTasks())).ToString();
					else
						response = BuildStandardResponse().ToString();
					
					_cmdTaskWaitEvent.Reset();

					responseBytes.AddRange(UTF8Encoding.UTF8.GetBytes(response));
                    mapSessionToConnectionDetails[_commandChannel].DataSent += responseBytes.Count();
                }
                catch(Exception ex)
                {
                    ServerComms.LogMessage($"ERROR Processing command channel message {ex.Message}");
                }
            }
            else
            {
                try
                {
					if (decryptedStatus == "closed")
                    {
                        ServerComms.LogMessage($"Close connection has been called on {decryptedSessionId}");
                        //Implant has called time
                        //cleanup the data queue

                        lock (_dataTasks)
                        {
							if (_dataTasks.ContainsKey(decryptedSessionId))
							{
								_dataTasks[decryptedSessionId].Tasks.Clear();
								var wait = _dataTasks[decryptedSessionId].Wait;
								_dataTasks[decryptedSessionId].DisposeWait();
								_dataTasks.Remove(decryptedSessionId);
							}
                        }

                        lock (_listenerLocker)
                        {
                            _listeners.Remove(decryptedSessionId);
                        }

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
                            ServerComms.LogMessage($"[Rx] {dtls.HostPort}:{dtls.Id} {uploadedPayload.Count()} bytes ");
                            SocksProxy.ReturnDataCallback(decryptedSessionId, this._encryption.Decrypt(uploadedPayload));
                        }
                    }
                    else
                    {
                        if (ServerComms.IsVerboseOn())
                            ServerComms.LogMessage($"Session ID {decryptedSessionId} is not valid");
                        ctx.Response.StatusCode = 404;
                        ctx.Response.OutputStream.Close();
                        return;
                    }

                    var ctr = 0;
					var dataQueue = _dataTasks[decryptedSessionId];
					var ready = false;

					while (null != dataQueue.Wait && !(ready = dataQueue.Wait.WaitOne(1000)) && ctr++ < _longpolltimeout) ;

					if (ready && dataQueue.Tasks.Count() > 0)
					{
						lock (dataQueue.PayloadLocker) { 
							while(dataQueue.Tasks.Count != 0)	
								responseBytes.AddRange(dataQueue.Tasks.Dequeue()); 
						}
						dataQueue.Wait.Reset();
						if (null != dtls)
							ServerComms.LogMessage($"[Tx] {dtls.HostPort}:{dtls.Id} {responseBytes.Count()} bytes ");
					}
					else
					if (null != dtls)
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

        XElement BuildStandardResponse()
        {
            return new XElement("Response", new XElement("Success", 0));
        }


        void AddListener(String targetId, Listener listenerInst)
        {
            lock (_listenerLocker)
            {
                _listeners.Add(targetId, listenerInst);
            }
        }

        public bool IsListenerConnected(String sessionId)
        {
            bool connected = false;
            lock (_listenerLocker)
            {
                if (!_listeners.ContainsKey(sessionId))
                    return false;

                connected = (_listeners[sessionId].Status == ListenerStatus.Connected);
            }
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
            lock (_dataTasksLocker)
            {
                _dataTasks.Add(listenerGuid, new DataTask());
            }
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
            lock (_dataTasksLocker)
            { 
                if (!_dataTasks.ContainsKey(listenerGuid))
                    return false;
				var dtaTasks = _dataTasks[listenerGuid];
				lock (dtaTasks.PayloadLocker) { _dataTasks[listenerGuid].Tasks.Enqueue(payload); }
				dtaTasks.Wait.Set();
			}
            return true;
        }

        bool QueueCommandTask(XElement task)
        {
            if (!IsCommandChannelConnected)
            {
                return false;
            }

            lock (_commandLocker)
            {
                _commandTasks.Enqueue(task);
            }
			_cmdTaskWaitEvent.Set();
			return true;
        }

        List<XElement> PopQueueCommandTasks()
        {
            var lst = new List<XElement>();
            int i = 0;
            lock (_commandLocker)
            {
                while (_commandTasks.Count > 0 && i++ < CommandLimit)
                    lst.Add(_commandTasks.Dequeue());
            }
			
			return lst;
        }

        void ProcessCommandChannelTime()
        {
            if (!_lastTimeCommandChannelSeen.HasValue)
            {
                lock (_locker)
                {
                    if (!_lastTimeCommandChannelSeen.HasValue)
                    {
                        CmdChannelRunningEvent.Set();
                        mapSessionToConnectionDetails[_commandChannel].UpdateTime = (this._lastTimeCommandChannelSeen = DateTime.Now).ToString();
                    }
                }
            }
            else
                mapSessionToConnectionDetails[_commandChannel].UpdateTime = (this._lastTimeCommandChannelSeen = DateTime.Now).ToString();
        }
    }
}
