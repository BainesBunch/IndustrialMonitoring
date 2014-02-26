using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using HttpLibrary;

namespace HttpServerExampleV2
{
    public class Program
    {
        private static HttpServer server;
        private static OutputPort LedPin;


        public static void Main()
        {
            LedPin = new OutputPort(Pins.ONBOARD_LED, false);//on board led for blinking
            ServerCredentials credentials = new ServerCredentials("john", "admin", "admin");//server login credentials
            ServerConfiguration config = new ServerConfiguration("192.168.0.220", "255.255.255.0", "192.168.0.1",  80);//server configuration
            server = new HttpServer(config, credentials, 512, 256, @"\SD");//new server instance
            server.OnRequestReceived += new OnRequestReceivedDelegate(server_OnRequestReceived);//request received event fired upon client connection
            server.OnServerError += new OnErrorDelegate(server_OnServerError);//event fired when an error occurs
            server.Start();
            while (true)
            {
                //led blinking on and off while server listens
                LedPin.Write(true);
                Thread.Sleep(1000);
                LedPin.Write(false);
                Thread.Sleep(1000);
            }
        }

        static void server_OnServerError(object sender, OnErrorEventArgs e)
        {
            Debug.Print(e.EventMessage);
        }

        static void server_OnRequestReceived(object sender, OnRequestReceivedArgs e)
        {
            if (e.IsInMemoryCard)
            {
                server.Send(e.FileName);
            }
            else
            {
                server.SendNotFound();
            }
        }
    }
}
