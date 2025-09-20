namespace MobileGwDataSync.Core.Exceptions
{
    public class DataSourceException : SyncException
    {
        public DataSourceException(string message)
            : base(message, "DATA_SOURCE_ERROR") { }

        public DataSourceException(string message, Exception innerException)
            : base(message, innerException, "DATA_SOURCE_ERROR") { }
    }
}
