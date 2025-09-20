namespace MobileGwDataSync.Core.Exceptions
{
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
