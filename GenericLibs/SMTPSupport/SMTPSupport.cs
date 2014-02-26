using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.IO;
using System.Text;
using NS_SMTPClient;
using Bansky.SPOT.Mail;

namespace NS_SMTP_Wrapper
{
    public class SMTPClient_Wrapper
    {
        public string Username = "Peter@EmbeddedAT.com";
        public string UserPass = "master";
        public ushort Socket = 25;
        public string ServerName = "smtp.yourisp.com";

        public string MessageHeader = "Propane Bottle Alert";
        public string RecipientsName = "Peter Baines";
        public string RecipientsAddress = "Peter@EmbeddedAT.com";

        public string SenderName = "Peter Baines";
        public string SenderAddress = "Peter@EmbeddedAT.com";

        public SMTPClient_Wrapper(string Username, string UserPass, ushort Socket, string ServerName, string MessageHeader, string RecipientsName, string RecipientsAddress, string SenderName, string SenderAddress)
        {
            this.Username = Username;
            this.UserPass = UserPass;
            this.Socket = Socket;
            this.ServerName = ServerName;

            this.MessageHeader = MessageHeader;

            this.RecipientsName = RecipientsName;
            this.RecipientsAddress = RecipientsAddress;

            this.SenderName = SenderName;
            this.SenderAddress = SenderAddress;
        }
        public void SendMessage(String MyMessage)
        {

            // Defines the mail message
            MailMessage Message = new MailMessage();

            // Defines the sender
            Message.From = new MailAddress(this.SenderAddress, this.SenderName);

            // Defines the receiver
            Message.To.Add(new MailAddress(this.RecipientsAddress, this.RecipientsName));

            Message.Subject = this.MessageHeader;
            Message.Body = MyMessage;
            // Format body as HTML
            Message.IsBodyHtml = true;


            SmtpClient Sender = new SmtpClient(this.ServerName, this.Socket);
            try
            {

                // Authenicate to server
                Sender.Authenticate = true;
                Sender.Username = this.Username;
                Sender.Password = this.UserPass;

                // Send message
                Sender.Send(Message);
            }
            catch (SmtpException e)
            {
                // Exception handling here 
                Debug.Print(e.Message);
                Debug.Print("Error Code: " + e.ErrorCode.ToString());
            }
            finally
            {
                Sender.Dispose();
            }

        }
        public static SMTPClient_Wrapper ReadFromFile()
        {
            if (File.Exists(@"\SD\SMTPCredentials.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\SMTPCredentials.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                string Username = Reader.ReadLine();
                string UserPass = Reader.ReadLine();
                ushort Socket = ushort.Parse(Reader.ReadLine());
                string ServerName = Reader.ReadLine();
                string MessageHeader = Reader.ReadLine();
                string RecipientsName = Reader.ReadLine();
                string RecipientsAddress = Reader.ReadLine();
                string SenderName = Reader.ReadLine();
                string SenderAddress = Reader.ReadLine();
                Reader.Close();
                fs.Close();
                return new SMTPClient_Wrapper(Username, UserPass, Socket, ServerName, MessageHeader, RecipientsName, RecipientsAddress, SenderName, SenderAddress);
            }
            else
            {
                SMTPClient_Wrapper oCreds = new SMTPClient_Wrapper("", "", 0, "", "", "", "", "", "");
                WriteToFile(oCreds);
                return oCreds;
            }
        }
        public static void WriteToFile(SMTPClient_Wrapper Credentials)
        {
            FileStream fs = new FileStream(@"\SD\SMTPCredentials.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter Writer = new StreamWriter(fs);
            Writer.WriteLine(Credentials.Username);
            Writer.WriteLine(Credentials.UserPass);
            Writer.WriteLine(Credentials.Socket.ToString());
            Writer.WriteLine(Credentials.ServerName);
            Writer.WriteLine(Credentials.MessageHeader);
            Writer.WriteLine(Credentials.RecipientsName);
            Writer.WriteLine(Credentials.RecipientsAddress);
            Writer.WriteLine(Credentials.SenderName);
            Writer.WriteLine(Credentials.SenderAddress);
            Writer.Close();
            fs.Close();
        }

    }
}

