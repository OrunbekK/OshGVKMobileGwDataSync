namespace MobileGwDataSync.Core.Models.Configuration
{
    public class OneCSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Timeout { get; set; } = 300;

        public string HealthCheckEndpoint { get; set; } = "health";
        public int HealthCheckTimeout { get; set; } = 5;
    }
}
