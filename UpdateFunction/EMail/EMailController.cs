using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using azuregeek.AZAcronisUpdater.TableStorage.Models;

namespace azuregeek.AZAcronisUpdater.EMail
{
    public class EMailController
    {
        private string _mailServer;
        private int _mailServerPort;
        private SecureSocketOptions _secureOptions;
        private bool _authenticated;
        private string _username;
        private string _password;
        
        public EMailController(string mailServer, int port, SecureSocketOptions secureOptions = SecureSocketOptions.Auto, bool authenticated = false, string userName = null, string password = null)
        {
            _mailServer = mailServer;
            _mailServerPort = port;
            _secureOptions = secureOptions;
            _authenticated = authenticated;
            _username = userName;
            _password = password;            
        }

        public void sendMail(MimeMessage message)
        {
            var smtpClient = new SmtpClient();
            smtpClient.Connect(_mailServer, _mailServerPort, _secureOptions);
            if (_authenticated)
                smtpClient.Authenticate(_username, _password);
            smtpClient.Send(message);                   
        }

        public void sendAgentUpdateTable(MailboxAddress fromAddress, List<MailboxAddress> toAddresses, List<AgentUpdateEntity> updateTable)
        {
            string htmlUpdateTable = generateHtmlUpdateTable(updateTable);
            StringBuilder htmlBodyBuilder = new StringBuilder();

            htmlBodyBuilder.Append("<html>");
            htmlBodyBuilder.Append("<head>");
            htmlBodyBuilder.Append("</head>");
            htmlBodyBuilder.Append("<body>");
            htmlBodyBuilder.Append($"<p>Hi,<br />");
            htmlBodyBuilder.Append("<br />");
            htmlBodyBuilder.Append("the following Agents have been updated by <a href=\"https://github.com/TobiKr/AcronisAgentUpdater\">AcronisAgentUpdater</a>:<br />");
            htmlBodyBuilder.Append("<br />");
            htmlBodyBuilder.Append(htmlUpdateTable);            
            htmlBodyBuilder.Append("<p>Have a great day (or night)!<br />your Acronis Agent Updater :-)</p>");
            htmlBodyBuilder.Append("</body>");
            htmlBodyBuilder.Append("</html>");

            BodyBuilder bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = htmlBodyBuilder.ToString();
            bodyBuilder.TextBody = "Please switch to HTML view to show updated Agents.";

            MimeMessage message = new MimeMessage();
            message.From.Add(fromAddress);                    
            message.Body = bodyBuilder.ToMessageBody();

            foreach(MailboxAddress toAddress in toAddresses)
            {
                message.To.Add(toAddress);
            }

            if(updateTable.Count == 1)
                message.Subject = $"Acronis Agent Updater updated {updateTable.Count} Agent :-)";
            else
                message.Subject = $"Acronis Agent Updater updated {updateTable.Count} Agents :-)";

            sendMail(message);
        }

        private string generateHtmlUpdateTable(List<AgentUpdateEntity> updateTable)
        {

            StringBuilder htmlTableBuilder = new StringBuilder();
            htmlTableBuilder.Append("<table cellspacing=\"0\" cellpadding = \"5\" border = \"1\">");

            // table header
            htmlTableBuilder.Append("<tr>");
            htmlTableBuilder.Append($"<td style=\"white-space:nowrap;\">Parent Tenant</td>");
            htmlTableBuilder.Append($"<td style=\"white-space:nowrap;\">Tenant</td>");
            htmlTableBuilder.Append($"<td style=\"white-space:nowrap;\">Hostname</td>");
            htmlTableBuilder.Append($"<td style=\"white-space:nowrap;\">Agent OS</td>");
            htmlTableBuilder.Append($"<td style=\"white-space:nowrap;\">Version<br />before Update</td>");
            htmlTableBuilder.Append($"<td style=\"white-space:nowrap;\">Version<br />after Update</td>");
            htmlTableBuilder.Append("</tr>");

            // fill table
            foreach (AgentUpdateEntity entity in updateTable)
            {
                htmlTableBuilder.Append("<tr>");
                htmlTableBuilder.Append($"<td>{entity.ParentTenantName}</td>");
                htmlTableBuilder.Append($"<td>{entity.TenantName}</td>");
                htmlTableBuilder.Append($"<td>{entity.HostName}</td>");
                htmlTableBuilder.Append($"<td>{entity.AgentOS}</td>");
                htmlTableBuilder.Append($"<td>{entity.AgentVersionBeforeUpdate}</td>");
                htmlTableBuilder.Append($"<td>{entity.AgentVersionAfterUpdate}</td>");
                htmlTableBuilder.Append("</tr>");
            }
            htmlTableBuilder.Append("</table>");

            return htmlTableBuilder.ToString();
        }
    }
}
