namespace RS1_2024_25.API.Data.Models.Mail
{
    public class MailSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public bool UseDefaultCredentials { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

    }
}
