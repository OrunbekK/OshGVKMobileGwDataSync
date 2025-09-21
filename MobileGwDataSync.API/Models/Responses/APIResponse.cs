namespace MobileGwDataSync.API.Models.Responses
{
    /// <summary>
    /// TODO: Единый формат ответа для всех API endpoints
    /// </summary>
    public class APIResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static APIResponse<T> Ok(T data, string? message = null)
        {
            return new APIResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static APIResponse<T> Fail(string message, List<string>? errors = null)
        {
            return new APIResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }
}