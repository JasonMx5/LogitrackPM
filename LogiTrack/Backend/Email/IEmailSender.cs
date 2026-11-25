using System;
using System.Threading.Tasks;

namespace LogiTrack.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string recipient, string subject, string body);
    }

    public class SmtpOptions
    {
        public string Host { get; set; }
        public int Port { get; set; } = 587;
        public string UserName { get; set; }
        public string Password { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; } = "LogiTrack";
    }

    public sealed class EmailDeliveryException : Exception
    {
        public EmailDeliveryException(string message)
            : base(message)
        {
        }

        public EmailDeliveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
