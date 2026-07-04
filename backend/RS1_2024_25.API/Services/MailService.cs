using MailKit;
using Microsoft.Extensions.Options;
using RS1_2024_25.API.Data.Models.Mail;
using RS1_2024_25.API.Services.Interfaces;
using System.Net;
using System.Net.Mail;

namespace RS1_2024_25.API.Services
{
    public class MailService(IOptions<MailSettings> options) : IMyMailService
    {
        private MailSettings ms = options.Value;
        public async Task Send(MailData data)
        {
            var client = new SmtpClient(ms.Host, ms.Port)
            {
                EnableSsl = ms.EnableSsl,
                UseDefaultCredentials = ms.UseDefaultCredentials,
                Credentials = new NetworkCredential(ms.Email, ms.Password)
            };

            var msg = new MailMessage();
            msg.From = new MailAddress(ms.Email, ms.Username);
            msg.To.Add(new MailAddress(data.To));
            msg.Subject = data.Subject;
            msg.IsBodyHtml = data.IsBodyHtml;
            msg.Body = data.Body.ToString();
            
            await client.SendMailAsync(msg);
        }
    }
}
