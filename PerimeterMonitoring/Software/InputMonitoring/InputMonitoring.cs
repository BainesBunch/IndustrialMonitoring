using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.IO;

namespace NS_InputMonitoring
{
    public class InputMonitoring
    {

        public enum PinTypes
        {
            Door = 1,
            PIR = 2
        }

        public struct AlarmPoint
        {
            public Cpu.Pin PinNumber;
            public string PinName;
            public PinTypes PinType;
            public Boolean Triggerd;
            public Boolean Email;
            public Boolean EmailSent;
            public DateTime DateTimeTriggerd;
            public InterruptPort button;
        }
        
        public static AlarmPoint[] AlarmPoints = new AlarmPoint[15];
        
        public InputMonitoring(AlarmPoint[] myAlarmPoints)
        {
            AlarmPoints = myAlarmPoints;

            Cpu.Pin MyPin = Pins.GPIO_NONE;
            for (int iPin = 0; iPin < 14; iPin++)
            {

                switch (iPin)
                {
                    case  0:
                        MyPin = Pins.GPIO_PIN_D0;
                        break;
                    case  1:
                        MyPin = Pins.GPIO_PIN_D1;
                        break;
                    case  2:
                        MyPin = Pins.GPIO_PIN_D2;
                        break;
                    case  3:
                        MyPin = Pins.GPIO_PIN_D3;
                        break;
                    case  4:
                        MyPin = Pins.GPIO_PIN_D4;
                        break;
                    case  5:
                        MyPin = Pins.GPIO_PIN_D5;
                        break;
                    case  6:
                        MyPin = Pins.GPIO_PIN_D6;
                        break;
                    case  7:
                        MyPin = Pins.GPIO_PIN_D7;
                        break;
                    case  8:
                        MyPin = Pins.GPIO_PIN_D8;
                        break;
                    case  9:
                        MyPin = Pins.GPIO_PIN_D9;
                        break;
                    case  10:
                        MyPin = Pins.GPIO_PIN_D10;
                        break;
                    case  11:
                        MyPin = Pins.GPIO_PIN_D11;
                        break;
                    case  12:
                        MyPin = Pins.GPIO_PIN_D12;
                        break;
                    case  13:
                        MyPin = Pins.GPIO_PIN_D13;
                        break;
                }
                AlarmPoints[iPin].PinNumber = MyPin;
                AlarmPoints[iPin].button = new InterruptPort(MyPin, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLevelLow);
                AlarmPoints[iPin].button.OnInterrupt += new NativeEventHandler(button_OnInterrupt);
                //AlarmPoints[iPin].EmailSent = false;
                //AlarmPoints[iPin].Email = true;
                //AlarmPoints[iPin].Triggerd = false;
                //AlarmPoints[iPin].DateTimeTriggerd = new DateTime();
                //AlarmPoints[iPin].PinName = "Not Yet Allocated";
                //AlarmPoints[iPin].PinType = PinTypes.Door;
            }
        }
        
        public void ResetTrigger(int PointID)
        {
            AlarmPoints[PointID].Triggerd = false;
        }
        
        public static InputMonitoring ReadFromFile()
        {
            if (File.Exists(@"\SD\AlarmPins.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\AlarmPins.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                for (int iPin = 0; iPin < 14; iPin++)
                {
                    string myParams = Reader.ReadLine();
                    if (myParams != null)
                    {
                       string[] myParts = myParams.Split('~');
                       AlarmPoints[iPin].PinName = myParts[0];
                       AlarmPoints[iPin].PinType = (PinTypes) int.Parse(myParts[1]);
                       AlarmPoints[iPin].Email = (myParts[2] == "Y");
                    }
                }
                Reader.Close();
                fs.Close();
            }
            else
            {
                for (int iPin = 0; iPin < 14; iPin++)
                {
                    AlarmPoints[iPin].EmailSent = false;
                    AlarmPoints[iPin].Email = false;
                    AlarmPoints[iPin].Triggerd = false;
                    AlarmPoints[iPin].DateTimeTriggerd = new DateTime();
                    AlarmPoints[iPin].PinName = "Not Yet Allocated";
                    AlarmPoints[iPin].PinType = PinTypes.Door;
                }

                WriteToFile();
            }
            return new InputMonitoring(AlarmPoints);
        }

        public static void WriteToFile()
        {
            FileStream fs = new FileStream(@"\SD\AlarmPins.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter Writer = new StreamWriter(fs);

            AlarmPoint TempPoint;

            string myLineBuffer = "";

            for (int iPin = 0; iPin < 14; iPin++)
            {
                TempPoint = AlarmPoints[iPin];
                myLineBuffer = TempPoint.PinName;
                myLineBuffer += "~";
                myLineBuffer += TempPoint.PinType.ToString();
                myLineBuffer += "~";
                myLineBuffer += TempPoint.Email.ToString();
                Writer.WriteLine(myLineBuffer);
            }

            Writer.Close();
            fs.Close();
        }

        private static void button_OnInterrupt(uint TriggerPin, uint data, DateTime time)
	    {
            for (int iPin = 0; iPin < 14; iPin++)
            {
                if (AlarmPoints[iPin].PinNumber == (Cpu.Pin)TriggerPin)
                {
                    AlarmPoints[iPin].Triggerd = true;
                    AlarmPoints[iPin].DateTimeTriggerd = DateTime.Now;
                    AlarmPoints[iPin].button.ClearInterrupt();
                }
            }
	    }

    }
}
