using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace Proyecto_GCS.Utils
{
    public static class Mailer
    {
        public static async Task EnviarAsync(string to, string subject, string htmlBody)
        {
            var from = WebConfigurationManager.AppSettings["SmtpFrom"] ?? "no-reply@tuapp.local";
            using (var msg = new MailMessage())
            {
                msg.From = new MailAddress(from);
                msg.To.Add(to);
                msg.Subject = subject;
                msg.Body = htmlBody;
                msg.IsBodyHtml = true;

                using (var smtp = new SmtpClient()) // usa <system.net> del Web.config
                {
                    await smtp.SendMailAsync(msg);
                }
            }
        }
    }
}
