namespace MobileGwDataSync.Core.Exceptions
{
    public class SyncException : Exception
    {
        public string ErrorCode { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();

        public SyncException(string message, string errorCode = "SYNC_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public SyncException(string message, Exception innerException, string errorCode = "SYNC_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
