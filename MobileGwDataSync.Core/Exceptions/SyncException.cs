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

    public class DataSourceException : SyncException
    {
        public DataSourceException(string message)
            : base(message, "DATA_SOURCE_ERROR") { }

        public DataSourceException(string message, Exception innerException)
            : base(message, innerException, "DATA_SOURCE_ERROR") { }
    }

    public class DataTargetException : SyncException
    {
        public DataTargetException(string message)
            : base(message, "DATA_TARGET_ERROR") { }

        public DataTargetException(string message, Exception innerException)
            : base(message, innerException, "DATA_TARGET_ERROR") { }
    }

    public class ValidationException : SyncException
    {
        public List<string> ValidationErrors { get; set; } = new();

        public ValidationException(string message, List<string> errors)
            : base(message, "VALIDATION_ERROR")
        {
            ValidationErrors = errors;
        }
    }
}
