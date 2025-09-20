namespace MobileGwDataSync.Core.Models.Configuration
{
    public class EmailSettings
    {
        public bool Enabled { get; set; }
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string From { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
