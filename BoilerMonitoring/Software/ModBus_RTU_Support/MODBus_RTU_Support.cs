using System;
using System.Net;
using System.Text;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.IO;

namespace NS_MODBus_RTU_Support
{
    public class MODBus_RTU_Support
    {

        public static OutputPort TXEnable;
        public static OutputPort RXEnable;
        SerialPort Bus_Port;

        public string MTUPort;
        public int MTUBaud;
        public int MTUTXPin;
        public int MTURXPin;
        public byte MTUBusAddress;

        public static ArrayList StatusCodesLookup = new ArrayList();
        public static ArrayList BlockingCodesLookup = new ArrayList();

        public MODBus_RTU_Support(string Port, int Baud, Cpu.Pin TXPin, Cpu.Pin RXPin, byte BusAddress)
        {
            TXEnable = new OutputPort(Pins.GPIO_PIN_D2, true);
            RXEnable = new OutputPort(Pins.GPIO_PIN_D3, true);

            this.MTUBusAddress = BusAddress;

            Bus_Port = new SerialPort(Port, Baud, Parity.None, 8, StopBits.One);

            Bus_Port.WriteTimeout = 5000;
            Bus_Port.ReadTimeout = 5000;
            Bus_Port.Open();

            StatusCodesLookup.Add("2|Heat Demand blocked due to high absolute outlet temperature");
            StatusCodesLookup.Add("3|Heat Demand blocked due to high absolute fl ue emperature");
            StatusCodesLookup.Add("4|Heat Demand blocked due to high absolute Delta T (Outlet - Inlet)");
            StatusCodesLookup.Add("7|Heat Demand blocked due to changed Personality Plug");
            StatusCodesLookup.Add("8|Heat Demand blocked due to Low 24 VAC");
            StatusCodesLookup.Add("9|Outdoor shutdown");
            StatusCodesLookup.Add("10|Block due to switch OFF boiler (ON/OFF of Display)");
            StatusCodesLookup.Add("12|Block due to line frequency");
            StatusCodesLookup.Add("16|Service function");
            StatusCodesLookup.Add("19|DHW function Storage Tank");
            StatusCodesLookup.Add("21|SH function Heat demand from Room Thermostat");
            StatusCodesLookup.Add("22|SH function Heat demand from Boiler Management ystem");
            StatusCodesLookup.Add("23|SH function Heat demand from Cascade");
            StatusCodesLookup.Add("30|Heat demand activated by Freeze Protection");
            StatusCodesLookup.Add("32|DHW Pump Delay");
            StatusCodesLookup.Add("33|SH Pump Delay");
            StatusCodesLookup.Add("34|No heat function (after pump delay)");
            StatusCodesLookup.Add("40|Lockout");

            BlockingCodesLookup.Add("0|No blocking");
            BlockingCodesLookup.Add("1|SH blocking");
            BlockingCodesLookup.Add("2|Blocking Due to Low 24 VAC Supply");
            BlockingCodesLookup.Add("3|Blocking due to General block");
            BlockingCodesLookup.Add("4|Blocking MRHL is open");
            BlockingCodesLookup.Add("5|Blocking due to Switched OFF boiler (Display ENTER switch)");
            BlockingCodesLookup.Add("6|Blocking due to wrong communication of Cascade");
            BlockingCodesLookup.Add("7|Blocking due to High Delta");
            BlockingCodesLookup.Add("8|Blocking due to High Flue Temperature");
            BlockingCodesLookup.Add("9|Blocking due to low 24 VAC supply");
            BlockingCodesLookup.Add("10|Blocking due to General Block");
            BlockingCodesLookup.Add("12|Blocking due to to line frequency");
            BlockingCodesLookup.Add("13|Blocking anti-cycling time");
            BlockingCodesLookup.Add("14|Storage Tank demand Blocked due to Fan problems");
            BlockingCodesLookup.Add("15|No system sensor connected and leader control present");
            BlockingCodesLookup.Add("16|Blocking due to outlet temperature limit");
            BlockingCodesLookup.Add("17|Fan min decreased due to low fl ame current");
            BlockingCodesLookup.Add("18|Limit max fan speed due to high Delta T");
            BlockingCodesLookup.Add("19|Limit max fan speed due to high fl ue temp");
            BlockingCodesLookup.Add("21|Blocking due to Switched Off boiler");
            BlockingCodesLookup.Add("24|Blocking due to high temperature rise");
            BlockingCodesLookup.Add("25|Blocking due to high fl ue temperature");
            BlockingCodesLookup.Add("26|Blocking due to high outlet water temperature");
            BlockingCodesLookup.Add("27|Blocking due to anti-cycling time");
            BlockingCodesLookup.Add("28|Blocking due to changed ID Plug");

        }

        public static MODBus_RTU_Support ReadFromFile()
        {
            if (File.Exists(@"\SD\MODBusCredentials.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\MODBusCredentials.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                string Port = Reader.ReadLine();
                string Baud = Reader.ReadLine();
                string TXPin = Reader.ReadLine();
                string RXPin = Reader.ReadLine();
                string BusAddress = Reader.ReadLine();
                Reader.Close();
                fs.Close();
                return new MODBus_RTU_Support(Port, int.Parse(Baud), (Cpu.Pin)int.Parse(TXPin), (Cpu.Pin)int.Parse(RXPin), byte.Parse(BusAddress));
            }
            else
            {
                MODBus_RTU_Support oCreds = new MODBus_RTU_Support("COM1", 9600, Pins.GPIO_PIN_D10, Pins.GPIO_PIN_D11, (int)1);
                WriteToFile(oCreds);
                return oCreds;
            }
        }

        public static void WriteToFile(MODBus_RTU_Support Credentials)
        {
            FileStream fs = new FileStream(@"\SD\MODBusCredentials.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter Writer = new StreamWriter(fs);
            Writer.WriteLine(Credentials.MTUPort);
            Writer.WriteLine(Credentials.MTUBaud.ToString());
            Writer.WriteLine(Credentials.MTUTXPin.ToString());
            Writer.WriteLine(Credentials.MTURXPin.ToString());
            Writer.WriteLine(Credentials.MTUBusAddress.ToString());
            Writer.Close();
            fs.Close();
        }

        public ushort ReadInputRegister(ushort RegisterID, byte Bank)
        {

            ushort Retval = 0;

            byte[] WriteBuffer = new byte[8];
            byte[] ResponseBuffer = new byte[200];

            //              Input Registers - Bank 3
            //30001 Discrete Inputs 1 - 16  0 65535 
            //30002 Discrete Inputs 17 - 32 0 65535 
            //30003 Discrete Inputs 33 - 48 0 65535 
            //30004 System / Cascade Setpoint Degrees Celsius 0 130
            //30005 System Pump Speed  % 0 100
            //30006 Cascade Total Power  % 100 800 
            //30007 Cascade Current Power  % 0 800
            //30008 Outlet Setpoint  Degrees Celsius 0 130
            //30009 Outlet Temperature  Degrees Celsius  130
            //30010 Inlet Temperature  Degrees Celsius -20 130 
            //30011 Flue Temperature  Degrees Celsius -20 130 
            //30012 Firing Rate  % 0 100 
            //30013 Boiler Pump Speed  % 0 100 
            //30014 Boiler Status Code  0 65535 
            //30015 Boiler Blocking Code  0 65535 
            //30016 Boiler Lockout Code


            //                   Holding Registers  - Bank 4
            //40001 Confi guration 0 65535
            //40002 Coils 0 65535
            //40003 0-10 Volt Input / Rate Command / SetpointCommand  % 0 100
            //40004 Tank Setpoint  Degrees Celsius 0 87,5
            //40005 Tank Temperature Degrees Celsius -20 130
            //40006 Outdoor Temperature Degrees Celsius -40 60
            //40007 System Supply Temperature Degrees Celsius -20 130
            //40008 System Return Temperature Degrees Celsius -20 130


            AssembleIncomingRegisterValuesBlock(this.MTUBusAddress, Bank, RegisterID, (ushort)1, ref WriteBuffer);
            //Set the Line drivers into Transmit mode
            TXEnable.Write(true);
            RXEnable.Write(true);

            //Give the line drivers time to settle down
            Thread.Sleep(250);

            //Write the buffer to the port
            Bus_Port.Write(WriteBuffer, 0, WriteBuffer.Length);

            //Wait until the TX buffer is empty
            while (Bus_Port.BytesToWrite > 0) { Thread.Sleep(5); };

            //Set the Line drivers into recieve mode
            TXEnable.Write(false);
            RXEnable.Write(false);

            //Give the line drivers time to settle down
            Thread.Sleep(250);

            int ResponseBufferPointer = 0;

            while (Bus_Port.BytesToRead > 0)
            {

                Bus_Port.Read(ResponseBuffer, ResponseBufferPointer, 1);
                ResponseBufferPointer++;
            }

            if (ResponseBuffer[0] == this.MTUBusAddress && ResponseBuffer[1] == Bank && ResponseBuffer[2] == 2)
            {
                Retval = (ushort)(ResponseBuffer[3] * 255 + ResponseBuffer[4]);
            }

            return Retval;

        }

        public string GetStatusCode(byte CodeID)
        {
            foreach (string value in StatusCodesLookup)
            {
                string[] Parts = value.Split('|');
                if (Parts[0] == CodeID.ToString()) return Parts[1];
            }
            return "Code not found";
        }

        public string GetBlockingCode(byte CodeID)
        {

            foreach (string value in BlockingCodesLookup)
            {
                string[] Parts = value.Split('|');
                if (Parts[0] == CodeID.ToString()) return Parts[1];
            }
            return "Code not found";
        }

        void AssembleIncomingRegisterValuesBlock(byte address, byte type, ushort start, ushort registers, ref byte[] IncomingRegisterValuesBuffer)
        {

            byte[] CRCBuffer = new byte[2];

            IncomingRegisterValuesBuffer[0] = address;
            IncomingRegisterValuesBuffer[1] = type;
            IncomingRegisterValuesBuffer[2] = (byte)(start >> 8);
            IncomingRegisterValuesBuffer[3] = (byte)start;
            IncomingRegisterValuesBuffer[4] = (byte)(registers >> 8);
            IncomingRegisterValuesBuffer[5] = (byte)registers;

            CalculateCRC(IncomingRegisterValuesBuffer, ref CRCBuffer);
            IncomingRegisterValuesBuffer[IncomingRegisterValuesBuffer.Length - 2] = CRCBuffer[0];
            IncomingRegisterValuesBuffer[IncomingRegisterValuesBuffer.Length - 1] = CRCBuffer[1];
        }

        void CalculateCRC(byte[] WriteBuffer, ref byte[] CRC)
        {
            ushort CRCFull = 0xFFFF;
            byte CRCHigh = 0xFF, CRCLow = 0xFF;
            char CRCLSB;

            for (int i = 0; i < (WriteBuffer.Length) - 2; i++)
            {
                CRCFull = (ushort)(CRCFull ^ WriteBuffer[i]);

                for (int j = 0; j < 8; j++)
                {
                    CRCLSB = (char)(CRCFull & 0x0001);
                    CRCFull = (ushort)((CRCFull >> 1) & 0x7FFF);

                    if (CRCLSB == 1)
                        CRCFull = (ushort)(CRCFull ^ 0xA001);
                }
            }
            CRC[1] = CRCHigh = (byte)((CRCFull >> 8) & 0xFF);
            CRC[0] = CRCLow = (byte)(CRCFull & 0xFF);
        }

    }
}








