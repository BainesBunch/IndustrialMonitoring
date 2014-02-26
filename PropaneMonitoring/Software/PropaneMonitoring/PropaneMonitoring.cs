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

namespace NS_PropaneMonitoring
{


    public class PropaneMonitoring
    {
        private static TinyWS myServer;
        private static OutputPort LedGREENPin;
        private static OutputPort LedREDPin;
        private static OutputPort LedBLUEPin;

        private static SecretLabs.NETMF.Hardware.AnalogInput PropaneLevelSensorPort;
        private static SecretLabs.NETMF.Hardware.AnalogInput PropaneLeakSensorPort;

        private static int intPropaneLevelReading = 0;
        private static int intPropaneLeakReading = 0;


        private static int LevelTriggerPoint = 20;
        private static int LeakTriggerPoint = 20;

        private static SMTPClient_Wrapper Mailer = null;

        private static bool LeakMessageSent = false;
        private static bool LevelMessageSent = false;

        private static int State = 0; // 1 = OK, 2 = Low Gas, 3 = Leak


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


            PropaneLevelSensorPort = new SecretLabs.NETMF.Hardware.AnalogInput(Pins.GPIO_PIN_A0);
            PropaneLeakSensorPort = new SecretLabs.NETMF.Hardware.AnalogInput(Pins.GPIO_PIN_A1);

            LedREDPin = new OutputPort(Pins.GPIO_PIN_D5, false);
            LedGREENPin = new OutputPort(Pins.GPIO_PIN_D6, false);
            LedBLUEPin = new OutputPort(Pins.GPIO_PIN_D7, false);


            ServerCredentials credentials = ServerCredentials.ReadFromFile();

            ServerConfiguration config = ServerConfiguration.ReadFromFile();

            Mailer = SMTPClient_Wrapper.ReadFromFile();
         
            myServer = new TinyWS(config, credentials,2048,512, @"\SD");



            ReadTriggerPoints();

            myServer.OnRequestReceived += new OnRequestReceivedDelegate(server_OnRequestReceived);//request received event fired upon client connection
            myServer.OnServerError += new OnErrorDelegate(server_OnServerError);//event fired when an error occurs

            myServer.Start();

            while (true)
            {

                State = 0;

                intPropaneLevelReading = ProcessLevelReading(PropaneLevelSensorPort.Read());
                intPropaneLeakReading = ProcessLeakReading(PropaneLeakSensorPort.Read());

                if (intPropaneLevelReading < LevelTriggerPoint)
                {
                    State = 1;
                    if (!LevelMessageSent)
                    {
                        LevelMessageSent = true;
                        //Mailer.SendMessage("The Propane Tank is below the alarm level of " + LevelTriggerPoint.ToString() + " It is currently reading " + intPropaneLevelReading.ToString());
                    }
                }

                if (intPropaneLeakReading > LeakTriggerPoint)
                {
                    State = 2;
                    if (! LeakMessageSent)
                    {
                        LeakMessageSent = true;
                        //Mailer.SendMessage("The system has detected a gas leak \n It is currently reading " + intPropaneLeakReading.ToString());
                    }
                }

                //led blinking on and off while server listens

                OutputPort op = LedGREENPin;

                switch (State)
                {
                    case 0:
                        op = LedGREENPin;
                        break;
                    case 1:
                        op = LedBLUEPin;
                        break;
                    case 2:
                        op = LedREDPin;
                        break;
                }

                op.Write(true);
                Thread.Sleep(500);
                op.Write(false);
                Thread.Sleep(500);
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
            
            if (REQUEST.IndexOf("ReadSensors") >= 0)
            {
                string strSensorReadings = intPropaneLevelReading.ToString() + ":" + intPropaneLeakReading.ToString();
                myServer.SendAJAX(strSensorReadings);
            }

            else if (REQUEST.IndexOf("ReadTriggerPoints") >= 0)
            {
                string strSensorReadings = LevelTriggerPoint.ToString() + ":" + LeakTriggerPoint.ToString();
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
                        LevelTriggerPoint = int.Parse(Payload[0].Trim());
                        StoreTriggerPoints();
                        break;
                    }
                }
                myServer.SendAJAX("OK");
            }

            else if (REQUEST.IndexOf("LeakDetectionValueChange") >= 0)
            {
                string[] parts = REQUEST.Split('\r');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (replace(parts[i], "\n", "") == null)
                    {
                        string[] Payload = replace(parts[i + 1], "\n", "").Split(':');
                        LeakTriggerPoint = int.Parse(Payload[0].Trim());
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
                    if ( replace(parts[i], "\n" , "")  == null)
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
                LevelMessageSent = false;
                LeakMessageSent = false;
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
            Writer.WriteLine(LevelTriggerPoint);
            Writer.WriteLine(LeakTriggerPoint);
            Writer.Close();
            fs.Close();
        }



        static void ReadTriggerPoints()
        {
            if (File.Exists(@"\SD\TriggerPoints.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\TriggerPoints.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                LevelTriggerPoint = int.Parse(Reader.ReadLine().Trim());
                LeakTriggerPoint = int.Parse(Reader.ReadLine().Trim());
                Reader.Close();
                fs.Close();
            }
            else
            {
                LevelTriggerPoint = 20;
                LeakTriggerPoint = 100;
                StoreTriggerPoints();
            }
        }

        static int ProcessLevelReading(float Raw)
        {
            int retval = (int) (Raw * .0966789);
            return retval;
        }

        static int ProcessLeakReading(float Raw)
        {
            int retval = (int)(Raw * .0966789);
            return retval;
        }
    }
}

