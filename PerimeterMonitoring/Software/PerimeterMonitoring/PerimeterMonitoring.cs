using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.IO;
using System.Text;
using NS_TinyWS;
using NS_SMTP_Wrapper;
using NS_InputMonitoring;
using Microsoft.SPOT.Net.NetworkInformation;

namespace NS_PerimeterMonitor
{
    public class PerimeterMonitor
    {
        private static TinyWS myServer;
        private static SMTPClient_Wrapper Mailer;
        private static InputMonitoring AlarmStates;
        private static NetworkInterface NI;

        public static void Main()
        {

            NI = Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0];
            NI.EnableDhcp();
            int sec = 0;
            while (NI.IPAddress == "0.0.0.0")
            {
                string msg = "Waiting for DHCP for " + sec + " sec.  IP=" + NI.IPAddress.ToString();
                Debug.Print(msg);
                Thread.Sleep(5000);
                sec += 5;
            }
            Debug.Print("Got IP" + NI.IPAddress.ToString());

            Ntp.UpdateTimeFromNtpServer("time.nist.gov", 1);

            ServerCredentials credentials = ServerCredentials.ReadFromFile();

            ServerConfiguration config = ServerConfiguration.ReadFromFile();

            AlarmStates = InputMonitoring.ReadFromFile();

            Mailer = SMTPClient_Wrapper.ReadFromFile();

            myServer = new TinyWS(config, credentials, 1024, 1024, @"\SD");

            myServer.OnRequestReceived += new OnRequestReceivedDelegate(server_OnRequestReceived);//request received event fired upon client connection
            myServer.OnServerError += new OnErrorDelegate(server_OnServerError);//event fired when an error occurs

            myServer.Start();

            
            while (true)
            {

                for (int iPin = 0; iPin < 14; iPin++)
                {
                    if(InputMonitoring.AlarmPoints[iPin].Triggerd)
                    {
                        if (InputMonitoring.AlarmPoints[iPin].Email)
                        {
                            if(! InputMonitoring.AlarmPoints[iPin].EmailSent)
                            {
                                InputMonitoring.AlarmPoints[iPin].EmailSent = true;
                                //Mailer.SendMessage("The Perimiter alarm system has detected a trigger on " + InputMonitoring.AlarmPoints[iPin].PinName + " at " + InputMonitoring.AlarmPoints[iPin].DateTimeTriggerd.ToString());
                            }
                            InputMonitoring.WriteToFile();
                        }
                    }
                }
                                
                Thread.Sleep(500); // we dont want to be swamping out the server requests
            }
        }

        static void server_OnServerError(object sender, OnErrorEventArgs e)
        {
            Debug.Print(e.EventMessage);
        }

        static void server_OnRequestReceived(object sender, OnRequestReceivedArgs e)
        {

            string REQUEST = new string(UTF8Encoding.UTF8.GetChars(e.Request));

            //  Ajax calls
            //
            //  ReadTriggerPoints, ReadSensors, ReadSMTPDetails to read the settings and values on the device
            //
            //  TankLevelValueChange, LeakDetectionValueChange, SMTPChange, SMTPReset to send values from the form to the device
            //
            if (REQUEST.IndexOf("ReadStates") >= 0)
            {

                string strSensorReadings = "";

                for (int iPin = 0; iPin < 14; iPin++)
                {
                    if (strSensorReadings.Length > 0) strSensorReadings += "~";

                    if (InputMonitoring.AlarmPoints[iPin].Triggerd)
                    {
                        strSensorReadings += "YES";
                        strSensorReadings += "^";
                        strSensorReadings += InputMonitoring.AlarmPoints[iPin].DateTimeTriggerd.ToString();
                    }
                    else
                    {
                        strSensorReadings += "NO";
                        strSensorReadings += "^";
                    }
                }                
                
                myServer.SendAJAX(strSensorReadings);
            }
            else if (REQUEST.IndexOf("ReadSMTPDetails") >= 0)
            {
                string strSensorReadings = Mailer.ServerName + ":" + Mailer.Username + ":" + Mailer.UserPass + ":" + Mailer.RecipientsAddress;
                myServer.SendAJAX(strSensorReadings);
            }
            else if (REQUEST.IndexOf("ReadSettings") >= 0)
            {

                string strSensorReadings = "";

                for (int iPin = 0; iPin < 14; iPin++)
                {
                    if (strSensorReadings.Length > 0) strSensorReadings += "~";

                    strSensorReadings += iPin.ToString();

                    strSensorReadings += "^";

                    strSensorReadings += InputMonitoring.AlarmPoints[iPin].PinName;

                    strSensorReadings += "^";

                    strSensorReadings += InputMonitoring.AlarmPoints[iPin].PinType;

                    strSensorReadings += "^";


                    if (InputMonitoring.AlarmPoints[iPin].Email)
                    {
                        strSensorReadings += "Y";
                    }
                    else
                    {
                        strSensorReadings += "N";
                    }

                }

                myServer.SendAJAX(strSensorReadings);
            }
            else if (REQUEST.IndexOf("ReadSMTPDetails") >= 0)
            {
                string strSensorReadings = Mailer.ServerName + ":" + Mailer.Username + ":" + Mailer.UserPass + ":" + Mailer.RecipientsAddress;
                myServer.SendAJAX(strSensorReadings);
            }

            else if (REQUEST.IndexOf("UpdateInputs") >= 0)
            {
                try
                {
                    string[] parts = REQUEST.Split('\r');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (replace(parts[i], "\n", "") == null)
                        {
                            string[] Items = replace(parts[i + 1], "\n", "").Split(':');
                            int iPin = int.Parse(Items[0]);
                            InputMonitoring.AlarmPoints[iPin].PinName = Items[1];
                            InputMonitoring.AlarmPoints[iPin].PinType = (InputMonitoring.PinTypes)int.Parse(Items[2]);
                            InputMonitoring.AlarmPoints[iPin].Email = (Items[3] == "Y");
                            InputMonitoring.WriteToFile();
                            }
                        }
                }
                catch { }
                myServer.SendAJAX("OK");
            }


            else if (REQUEST.IndexOf("TriggerReset") >= 0)
            {
                string[] parts = REQUEST.Split('\r');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (replace(parts[i], "\n", "") == null)
                    {
                        string Payload = replace(parts[i + 1], "\n", "");
                        AlarmStates.ResetTrigger(int.Parse(Payload));
                        break;
                    }
                }
                myServer.SendAJAX("OK");
            }
            else if (REQUEST.IndexOf("SMTPChange") >= 0)
            {
                string[] parts = REQUEST.Split('\r');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (replace(parts[i], "\n", "") == null)
                    {
                        string[] Payload = replace(parts[i + 1], "\n", "").Split(':');
                        Mailer.ServerName = Payload[0];
                        Mailer.Username = Payload[1];
                        Mailer.UserPass = Payload[2];
                        Mailer.RecipientsAddress = Payload[3];
                        SMTPClient_Wrapper.WriteToFile(Mailer);
                        break;
                    }
                }
                myServer.SendAJAX("OK");
            }
            else if (REQUEST.IndexOf("SMTPReset") >= 0)
            {
                myServer.SendAJAX("OK");
            }
            else // we must be rendering page content
            {
                if (File.Exists(e.FileName))
                {
                    myServer.Send(e.FileName);
                }
                else
                {
                    myServer.SendNotFound();
                }
            }
        }

        static string replace(string str, string unwanted, string replacement)
        {
            StringBuilder sb = new StringBuilder(str);
            sb.Replace(unwanted, replacement);
            return sb.ToString();
        }
        
    }
}

