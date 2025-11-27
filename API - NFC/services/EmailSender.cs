using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace API___NFC.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string htmlBody, string textBody = null);
    }

    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody, string textBody = null)
        {
            var message = new MimeMessage();
            var fromName = _config["Email:SenderName"] ?? "NFC";
            var fromAddress = _config["Email:From"] ?? "no-reply@midominio.com";
            message.From.Add(new MailboxAddress(fromName, fromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody ?? StripHtml(htmlBody)
            };

            message.Body = builder.ToMessageBody();

            var host = _config["Email:Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Email:Smtp:Host no está configurado.");

            var port = int.TryParse(_config["Email:Smtp:Port"], out var p) ? p : 587;
            var user = _config["Email:Smtp:User"];
            var pass = _config["Email:Smtp:Pass"];
            var useSsl = bool.TryParse(_config["Email:Smtp:UseSsl"], out var ssl) ? ssl : true;

            using var smtp = new SmtpClient();
            smtp.Timeout = 20000; // 20s

            // Escoge opciones SSL según puerto/flag
            SecureSocketOptions secureOption;
            if (port == 465)
            {
                // puerto SMTPS (SSL on connect)
                secureOption = SecureSocketOptions.SslOnConnect;
            }
            else if (useSsl)
            {
                // puerto STARTTLS típico (587)
                secureOption = SecureSocketOptions.StartTls;
            }
            else
            {
                secureOption = SecureSocketOptions.Auto;
            }

            try
            {
                await smtp.ConnectAsync(host, port, secureOption);

                // Si no hay credenciales no autenticamos (algunos SMTP lo permiten)
                if (!string.IsNullOrEmpty(user))
                {
                    // evitar XOAUTH2 si no se usa
                    smtp.AuthenticationMechanisms.Remove("XOAUTH2");
                    await smtp.AuthenticateAsync(user, pass);
                }

                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Log completo para debugging (servidor)
                Console.Error.WriteLine($"[EmailSender] Error enviando correo a {to}: {ex}");
                throw; // re-lanzar para que el caller lo capture y actúe en consecuencia
            }
        }

        private string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        }
    }
}
