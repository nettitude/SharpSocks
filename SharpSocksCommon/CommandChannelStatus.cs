namespace SharpSocksCommon
{
    public enum CommandChannelStatus
    {
        OPENING,
        OPEN,
        CONNECTED,
        CLOSING,
        CLOSED,
        NO_CHANGE,
        TIMEOUT,
        FAILED,
        ASYNC_UPLOAD
    }
}