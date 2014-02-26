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
using NS_MODBus_RTU_Support;

namespace PropaneMonitor
{

    public class Program
    {
        private static TinyWS myServer;
        private static SMTPClient_Wrapper Mailer;
        private static MODBus_RTU_Support ModBus;

        private static int OutdoorTemperature = 0;
        private static int TankTemperature = 0;
        private static string BoilerStatusCode = "";
        private static string BoilerBlockingCode = "";
        
        private static int MinTankTemp = 20;
        private static bool TempMessageSent = false;
        private static byte RegisterReaderRobin = 0;




        public static void Main()
        {
            Microsoft.SPOT.Net.NetworkInformation.NetworkInterface NI = Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0];
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

            ModBus = MODBus_RTU_Support.ReadFromFile();

            Mailer = SMTPClient_Wrapper.ReadFromFile();

            myServer = new TinyWS(config, credentials, 2048, 512, @"\SD");

            ReadTriggerPoints();

            myServer.OnRequestReceived += new OnRequestReceivedDelegate(server_OnRequestReceived);//request received event fired upon client connection
            myServer.OnServerError += new OnErrorDelegate(server_OnServerError);//event fired when an error occurs

            myServer.Start();

            while (true)
            {


                RegisterReaderRobin++;

                RegisterReaderRobin %= 4;

                //1 Read Coil Status
                //2 Read Input Status
                //3 Read Input Registers
                //4 Read Holding Registers

                switch (RegisterReaderRobin)
                {
                    case 0:
                        BoilerStatusCode = ModBus.GetStatusCode((byte)ModBus.ReadInputRegister(14, 3));
                        break;

                    case 1:
                        BoilerBlockingCode = ModBus.GetBlockingCode((byte)ModBus.ReadInputRegister(15, 3));
                        break;

                    case 2:
                        TankTemperature = ModBus.ReadInputRegister(5, 4);
                        break;

                    case 3:
                        OutdoorTemperature = ModBus.ReadInputRegister(6, 4);
                        break;
                }

                if (TankTemperature <= MinTankTemp)
                {
                    if (!TempMessageSent)
                    {
                        TempMessageSent = true;
                        //Mailer.SendMessage("The system has detected a low water tank temperature \n It is currently reading " + TankTemperature.ToString());
                    }
                }
                Thread.Sleep(500); // we dont want to be bombardig the boiler with requests
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

            if (REQUEST.IndexOf("ReadRegisters") >= 0)
            {
                string strSensorReadings = OutdoorTemperature.ToString() + ":" + TankTemperature.ToString() + ":" + BoilerStatusCode + ":" + BoilerBlockingCode;
                myServer.SendAJAX(strSensorReadings);
            }

            else if (REQUEST.IndexOf("ReadTriggerPoints") >= 0)
            {
                string strSensorReadings = MinTankTemp.ToString();
                myServer.SendAJAX(strSensorReadings);
            }


            else if (REQUEST.IndexOf("ReadSMTPDetails") >= 0)
            {
                string strSensorReadings = Mailer.ServerName + ":" + Mailer.Username + ":" + Mailer.UserPass + ":" + Mailer.RecipientsAddress;
                myServer.SendAJAX(strSensorReadings);
            }

            else if (REQUEST.IndexOf("TankLevelValueChange") >= 0)
            {
                string[] parts = REQUEST.Split('\r');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (replace(parts[i], "\n", "") == null)
                    {
                        string[] Payload = replace(parts[i + 1], "\n", "").Split(':');
                        MinTankTemp = int.Parse(Payload[0].Trim());
                        StoreTriggerPoints();
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
                TempMessageSent = false;
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

        static void StoreTriggerPoints()
        {
            FileStream fs = new FileStream(@"\SD\TriggerPoints.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter Writer = new StreamWriter(fs);
            Writer.WriteLine(MinTankTemp);
            Writer.Close();
            fs.Close();
        }

        static void ReadTriggerPoints()
        {
            if (File.Exists(@"\SD\TriggerPoints.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\TriggerPoints.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                MinTankTemp = int.Parse(Reader.ReadLine().Trim());
                Reader.Close();
                fs.Close();
            }
            else
            {
                MinTankTemp = 100;
                StoreTriggerPoints();
            }
        }

        static int ProcessLevelReading(float Raw)
        {
            int retval = (int)(Raw * .0966789);
            return retval;
        }

        static int ProcessLeakReading(float Raw)
        {
            int retval = (int)(Raw * .0966789);
            return retval;
        }
    }
}

