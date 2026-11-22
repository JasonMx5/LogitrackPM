using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LogiTrack.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _options;

        public SmtpEmailSender(IOptions<SmtpOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(string recipient, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(_options.Host) ||
                string.IsNullOrWhiteSpace(_options.FromEmail) ||
                _options.Port < 1 ||
                _options.Port > 65535 ||
                _options.Port == 465)
            {
                throw new EmailDeliveryException("SMTP delivery is not configured correctly.");
            }

            if (string.IsNullOrWhiteSpace(_options.UserName) !=
                string.IsNullOrWhiteSpace(_options.Password))
            {
                throw new EmailDeliveryException(
                    "SMTP authentication requires both a user name and password.");
            }

            try
            {
                using (var mail = new MailMessage())
                using (var client = new SmtpClient(_options.Host, _options.Port))
                {
                    mail.From = new MailAddress(
                        _options.FromEmail,
                        _options.FromName ?? "LogiTrack");
                    mail.To.Add(new MailAddress(recipient));
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = false;

                    client.EnableSsl = true;
                    client.Timeout = 10000;
                    if (!string.IsNullOrWhiteSpace(_options.UserName))
                    {
                        client.Credentials = new NetworkCredential(
                            _options.UserName,
                            _options.Password);
                    }

                    await client.SendMailAsync(mail);
                }
            }
            catch (SmtpException exception)
            {
                throw new EmailDeliveryException("SMTP email delivery failed.", exception);
            }
            catch (FormatException exception)
            {
                throw new EmailDeliveryException("SMTP email settings are invalid.", exception);
            }
        }
    }
}
