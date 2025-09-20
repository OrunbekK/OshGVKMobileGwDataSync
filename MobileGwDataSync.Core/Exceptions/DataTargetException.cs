namespace MobileGwDataSync.Core.Exceptions
{
    public class DataTargetException : SyncException
    {
        public DataTargetException(string message)
            : base(message, "DATA_TARGET_ERROR") { }

        public DataTargetException(string message, Exception innerException)
            : base(message, innerException, "DATA_TARGET_ERROR") { }
    }
}
