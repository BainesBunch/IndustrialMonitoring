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

namespace NS_TinyWS
{
    // Delegates for Server events

    public delegate void OnErrorDelegate(object sender, OnErrorEventArgs e);
    public delegate void OnRequestReceivedDelegate(object sender, OnRequestReceivedArgs e);

    // encapsulation of ServerCredentials
    public class ServerCredentials
    {

        public string ServerOwner;

        public string UserName;

        public string Password;

        public string Key;

        public ServerCredentials(string ServerOwner, string UserName, string Password)
        {
            this.ServerOwner = ServerOwner;
            this.UserName = UserName;
            this.Password = Password;
            this.Key = Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(UserName + ":" + Password));
        }

        public static ServerCredentials ReadFromFile()
        {
            if (File.Exists(@"\SD\ServerCredentials.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\ServerCredentials.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                string owner = Reader.ReadLine();
                string keeey = Reader.ReadLine();
                Reader.Close();
                fs.Close();
                string[] unpass = new string(UTF8Encoding.UTF8.GetChars(Convert.FromBase64String(keeey))).Split(':');
                return new ServerCredentials(owner, unpass[0], unpass[1]);
            }
            else
            {
                ServerCredentials oCreds = new ServerCredentials("Uknown Device", "admin", "admin");
                WriteToFile(oCreds);
                return oCreds;
            }
        }

        public static void WriteToFile(ServerCredentials Credentials)
        {
            FileStream fs = new FileStream(@"\SD\ServerCredentials.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter Writer = new StreamWriter(fs);
            Writer.WriteLine(Credentials.ServerOwner);
            Writer.WriteLine(Credentials.Key);
            Writer.Close();
            fs.Close();
        }
    }

    // encapsulation of ServerConfiguration
    public class ServerConfiguration
    {
        public string IpAddress;
        public string SubnetMask;
        public string DefaultGateWay;
        public int ListenPort;

        public ServerConfiguration(string IpAddress, string SubnetMask, string DefaultGateWay, int ListenPort)
        {
            this.IpAddress = IpAddress;
            this.SubnetMask = SubnetMask;
            this.DefaultGateWay = DefaultGateWay;
            this.ListenPort = ListenPort;
        }

        public static ServerConfiguration ReadFromFile()
        {
            if (File.Exists(@"\SD\ServerConfiguration.cfg"))
            {
                FileStream fs = new FileStream(@"\SD\ServerConfiguration.cfg", FileMode.Open, FileAccess.Read);
                StreamReader Reader = new StreamReader(fs);
                string myIpAddress = Reader.ReadLine();
                string mySubnetMask = Reader.ReadLine();
                string myDefaultGateWay = Reader.ReadLine();
                ushort myListenPort = ushort.Parse(Reader.ReadLine());
                Reader.Close();
                fs.Close();
                return new ServerConfiguration(myIpAddress, mySubnetMask, myDefaultGateWay, myListenPort);
            }
            else
            {

                ServerConfiguration MyNewCOnfig = new ServerConfiguration("192.168.0.220", "255.255.255.0", "192.168.0.1", 80);
                WriteToFile(MyNewCOnfig);
                return MyNewCOnfig;
            }
        }

        public static void WriteToFile(ServerConfiguration myServerConfiguration)
        {
            FileStream fs = new FileStream(@"\SD\ServerConfiguration.cfg", FileMode.Create, FileAccess.Write);
            StreamWriter Writer = new StreamWriter(fs);
            Writer.WriteLine(myServerConfiguration.IpAddress);
            Writer.WriteLine(myServerConfiguration.SubnetMask);
            Writer.WriteLine(myServerConfiguration.DefaultGateWay);
            Writer.WriteLine(myServerConfiguration.ListenPort.ToString());
            Writer.Close();
            fs.Close();
        }
    }
    
    // encapsulation of Error Events 
    public class OnErrorEventArgs : EventArgs
    {
        private string EVENT_MESSAGE;

        public string EventMessage
        {
            get { return EVENT_MESSAGE; }
        }

        public OnErrorEventArgs(string EVENT_MESSAGE)
        {
            this.EVENT_MESSAGE = EVENT_MESSAGE;
        }
    }

    // encapsulation of incomming Request event
    public class OnRequestReceivedArgs : EventArgs
    {
        private string FILE_NAME;
        private bool IS_IN_MMC;
        private byte[] REQUEST;

        public string FileName
        {
            get
            {
                return FILE_NAME;
            }
        }

        public bool IsInMemoryCard
        {
            get
            {
                return IS_IN_MMC;
            }
        }

        public byte[] Request
        {
            get
            {
                return REQUEST;
            }
        }

        public OnRequestReceivedArgs(string FILE_NAME, bool IS_IN_MMC, byte[] REQUEST)
        {
            this.FILE_NAME = FILE_NAME;
            this.IS_IN_MMC = IS_IN_MMC;
            this.REQUEST = REQUEST;
        }
    }

    // encapsulation of Server
    public class TinyWS
    {
        private Thread SERVER_THREAD;
        private Socket LISTEN_SOCKET;
        private Socket ACCEPTED_SOCKET;
        private bool IS_SERVER_RUNNING;
        private string STORAGE_PATH;
        private FileStream FILE_STREAM;
        private StreamReader FILE_READER;
        private StreamWriter FILE_WRITER;
        private byte[] RECEIVE_BUFFER;
        private byte[] SEND_BUFFER;
        private ServerConfiguration CONFIG;
        private ServerCredentials CREDENTIALS;
        private bool USE_AUTHENTICATION;
        private enum FileType { JPEG = 1, GIF = 2, Html = 3 };
        private string HtmlPageHeader = "HTTP/1.0 200 OK\r\nContent-Type: ";
        private string authheader = "HTTP/1.1 401 Authorization Required \nWWW-Authenticate: Basic realm=";
        private string Unauthorized = "<html><body><h1 align=center>" + "401 UNAUTHORIZED ACCESS</h1></body></html>";

        private void FragmentateAndSend(string FILE_NAME, FileType Type)
        {
            byte[] HEADER;
            long FILE_LENGTH;
            FILE_STREAM = new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read);
            FILE_READER = new StreamReader(FILE_STREAM);
            FILE_LENGTH = FILE_STREAM.Length;

            switch (Type)
            {
                case FileType.Html:
                    HEADER = UTF8Encoding.UTF8.GetBytes(HtmlPageHeader + "text/html" + "; charset=utf-8\r\nContent-Length: " + FILE_LENGTH.ToString() + "\r\n\r\n");
                    break;
                case FileType.GIF:
                    HEADER = UTF8Encoding.UTF8.GetBytes(HtmlPageHeader + "image/gif" + "; charset=utf-8\r\nContent-Length: " + FILE_LENGTH.ToString() + "\r\n\r\n");
                    break;
                case FileType.JPEG:
                    HEADER = UTF8Encoding.UTF8.GetBytes(HtmlPageHeader + "image/jpeg" + "; charset=utf-8\r\nContent-Length: " + FILE_LENGTH.ToString() + "\r\n\r\n");
                    break;
                default:
                    HEADER = UTF8Encoding.UTF8.GetBytes(HtmlPageHeader + "text/html" + "; charset=utf-8\r\nContent-Length: " + FILE_LENGTH.ToString() + "\r\n\r\n");
                    break;
            }

            ACCEPTED_SOCKET.Send(HEADER, 0, HEADER.Length, SocketFlags.None);

            while (FILE_LENGTH > SEND_BUFFER.Length)
            {
                FILE_STREAM.Read(SEND_BUFFER, 0, SEND_BUFFER.Length);
                ACCEPTED_SOCKET.Send(SEND_BUFFER, 0, SEND_BUFFER.Length, SocketFlags.None);
                FILE_LENGTH -= SEND_BUFFER.Length;
            }
            FILE_STREAM.Read(SEND_BUFFER, 0, (int)FILE_LENGTH);
            ACCEPTED_SOCKET.Send(SEND_BUFFER, 0, (int)FILE_LENGTH, SocketFlags.None);

            FILE_READER.Close();
            FILE_STREAM.Close();
        }

        private string GetFileName(string RequestStr)
        {
            RequestStr = RequestStr.Substring(RequestStr.IndexOf("GET /") + 5);
            RequestStr = RequestStr.Substring(0, RequestStr.IndexOf("HTTP"));
            return RequestStr.Trim();
        }

        private bool RequestContains(string Request, string Str)
        {
            return (Request.IndexOf(Str) >= 0);
        }

        private void BuildFileList(string[] FILES)
        {
            FILE_STREAM = new FileStream(STORAGE_PATH + "\\index.html", FileMode.Create, FileAccess.Write);
            FILE_WRITER = new StreamWriter(FILE_STREAM);
            FILE_WRITER.WriteLine("<html>");
            FILE_WRITER.WriteLine("<head>");
            FILE_WRITER.WriteLine("<title>");
            FILE_WRITER.WriteLine("Index Page");
            FILE_WRITER.WriteLine("</title>");
            FILE_WRITER.WriteLine("<body>");
            FILE_WRITER.WriteLine("<h1 align=center>");
            FILE_WRITER.WriteLine("FILE LIST");
            FILE_WRITER.WriteLine("</h1>");
            FILE_WRITER.WriteLine("<h1 align=center>");
            FILE_WRITER.WriteLine((FILES.Length - 2).ToString() + " FILES");
            FILE_WRITER.WriteLine("</h1>");
            foreach (string F in FILES)
            {
                if (!RequestContains(F, "index") && !RequestContains(F, "NotFound"))
                {
                    FILE_WRITER.WriteLine("<h1 align=center><a href=\"");
                    FILE_WRITER.WriteLine("/" + F.Substring(F.LastIndexOf("\\") + 1).ToLower() + "\">");
                    FILE_WRITER.WriteLine(F.Substring(F.LastIndexOf("\\") + 1).ToLower());
                    FILE_WRITER.WriteLine("</a>");
                }
            }
            FILE_WRITER.WriteLine("</body>");
            FILE_WRITER.WriteLine("</html>");
            FILE_WRITER.Close();
            FILE_STREAM.Close();
        }

        private bool IsFileFound(ref string FILE_NAME, string[] FILES)
        {
            foreach (string F in FILES)
            {
                if (RequestContains(F.ToLower(), FILE_NAME.ToLower()))
                {
                    FILE_NAME = F;
                    return true;
                }
            }
            //return false;
            return true;
        }

        private string GetFileExtention(string FILE_NAME)
        {
            string x = FILE_NAME;
            x = x.Substring(x.LastIndexOf('.') + 1);
            return x;
        }

        private void ProcessRequest()
        {
            string[] FILES;
            string REQUEST = "";
            string FILE_NAME = "";
            bool found = false;
            ACCEPTED_SOCKET.Receive(RECEIVE_BUFFER);
            if (USE_AUTHENTICATION)
            {
                if (Authenticate(RECEIVE_BUFFER))
                {
                    REQUEST = new string(UTF8Encoding.UTF8.GetChars(RECEIVE_BUFFER));
                    FILES = Directory.GetFiles(STORAGE_PATH);
                    FILE_NAME = GetFileName(REQUEST);
                    if (FILE_NAME == string.Empty) FILE_NAME = "\\SD\\index.html";
                    FILE_NAME = replace(FILE_NAME, "/", "\\");
                    if (FILE_NAME.IndexOf("\\SD") < 0) FILE_NAME = "\\SD\\" + FILE_NAME;
                    found = IsFileFound(ref FILE_NAME, FILES);
                    OnRequestReceivedFunction(new OnRequestReceivedArgs(FILE_NAME, found, RECEIVE_BUFFER));
                }
                else
                {
                    byte[] header = UTF8Encoding.UTF8.GetBytes(authheader + CREDENTIALS.ServerOwner + "\"\n\n");
                    ACCEPTED_SOCKET.Send(header, 0, header.Length, SocketFlags.None);
                    ACCEPTED_SOCKET.Send(UTF8Encoding.UTF8.GetBytes(Unauthorized), 0, Unauthorized.Length, SocketFlags.None);
                }
            }
            else
            {
                REQUEST = new string(UTF8Encoding.UTF8.GetChars(RECEIVE_BUFFER));
                FILES = Directory.GetFiles(STORAGE_PATH);
                FILE_NAME = GetFileName(REQUEST);
                found = IsFileFound(ref FILE_NAME, FILES);
                OnRequestReceivedFunction(new OnRequestReceivedArgs(FILE_NAME, found, RECEIVE_BUFFER));
            }
            for (int i = 0; i < RECEIVE_BUFFER.Length; i++) RECEIVE_BUFFER[i] = 0;
        }

        private void RunServer()
        {
            try
            {
                LISTEN_SOCKET = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint BindingAddress = new IPEndPoint(IPAddress.Any, CONFIG.ListenPort);
                LISTEN_SOCKET.Bind(BindingAddress);
                LISTEN_SOCKET.Listen(1);
                IS_SERVER_RUNNING = true;
                while (true)
                {
                    ACCEPTED_SOCKET = LISTEN_SOCKET.Accept();
                    ProcessRequest();
                    ACCEPTED_SOCKET.Close();
                }
            }
            catch (Exception)
            {
                IS_SERVER_RUNNING = false;
                OnServerErrorFunction(new OnErrorEventArgs("Server Error\r\nCheck Connection Parameters"));
            }
        }

        private bool Authenticate(byte[] request)
        {
            return RequestContains(new string(UTF8Encoding.UTF8.GetChars(request)), CREDENTIALS.Key);
        }

        private void ExecuteRequest()
        {
            string[] FILES;
            string REQUEST = "";
            string FILE_NAME = "";
            string FILE_EXTENTION = "";
            REQUEST = new string(UTF8Encoding.UTF8.GetChars(RECEIVE_BUFFER));
            FILES = Directory.GetFiles(STORAGE_PATH);
            FILE_NAME = GetFileName(REQUEST);
            if (FILE_NAME == "" || RequestContains(FILE_NAME, "index"))
            {
                BuildFileList(FILES);
                FragmentateAndSend(STORAGE_PATH + "\\index.Html", FileType.Html);
            }
            else
            {
                if (IsFileFound(ref FILE_NAME, FILES))
                {
                    FILE_EXTENTION = GetFileExtention(FILE_NAME.ToLower());
                    switch (FILE_EXTENTION)
                    {
                        case "gif":
                            FragmentateAndSend(FILE_NAME, FileType.GIF);
                            break;
                        case "txt":
                            FragmentateAndSend(FILE_NAME, FileType.Html);
                            break;
                        case "jpg":
                            FragmentateAndSend(FILE_NAME, FileType.JPEG);
                            break;
                        case "jpeg":
                            FragmentateAndSend(FILE_NAME, FileType.JPEG);
                            break;
                        case "htm":
                            FragmentateAndSend(FILE_NAME, FileType.Html);
                            break;
                        case "html":
                            FragmentateAndSend(FILE_NAME, FileType.Html);
                            break;
                        default:
                            FragmentateAndSend(FILE_NAME, FileType.Html);
                            break;
                    }
                }
                else
                {
                    FragmentateAndSend(STORAGE_PATH + "\\NotFound.Html", FileType.Html);
                }
            }
        }

        protected virtual void OnServerErrorFunction(OnErrorEventArgs e)
        {
            OnServerError(this, e);
        }

        protected virtual void OnRequestReceivedFunction(OnRequestReceivedArgs e)
        {
            OnRequestReceived(this, e);
        }

        public bool SecurityEnabled
        {
            get
            {
                return USE_AUTHENTICATION;
            }
        }

        public ServerConfiguration Configuration
        {
            get
            {
                return CONFIG;
            }
        }

        public bool IsServerRunning
        {
            get { return IS_SERVER_RUNNING; }
        }

        public Thread RunningThread
        {
            get { return SERVER_THREAD; }
        }

        public event OnErrorDelegate OnServerError;

        public event OnRequestReceivedDelegate OnRequestReceived;

        public TinyWS(ServerConfiguration Config, int ReceiveBufferSize, int SendBufferSize, string pages_folder)
        {
            SERVER_THREAD = null;
            LISTEN_SOCKET = null;
            ACCEPTED_SOCKET = null;
            IS_SERVER_RUNNING = false;
            STORAGE_PATH = pages_folder;
            RECEIVE_BUFFER = new byte[ReceiveBufferSize];
            SEND_BUFFER = new byte[SendBufferSize];
            SERVER_THREAD = new Thread(new ThreadStart(RunServer));
            CONFIG = Config;
            USE_AUTHENTICATION = false;

            if (!File.Exists(STORAGE_PATH + "\\index.html"))
            {
                FILE_STREAM = new FileStream(STORAGE_PATH + "\\index.html", FileMode.Create, FileAccess.Write);
                FILE_WRITER = new StreamWriter(FILE_STREAM);
                FILE_WRITER.WriteLine("<html>");
                FILE_WRITER.WriteLine("<head>");
                FILE_WRITER.WriteLine("<title>");
                FILE_WRITER.WriteLine("Index Page");
                FILE_WRITER.WriteLine("</title>");
                FILE_WRITER.WriteLine("<body>");
                FILE_WRITER.WriteLine("<h1 align=center>");
                FILE_WRITER.WriteLine("FILE LIST");
                FILE_WRITER.WriteLine("</h1>");
                FILE_WRITER.WriteLine("</body>");
                FILE_WRITER.WriteLine("</html>");
                FILE_WRITER.Close();
                FILE_STREAM.Close();
            }
            if (!File.Exists(STORAGE_PATH + "\\NotFound.html"))
            {
                FILE_STREAM = new FileStream(STORAGE_PATH + "\\NotFound.html", FileMode.Create, FileAccess.Write);
                FILE_WRITER = new StreamWriter(FILE_STREAM);
                FILE_WRITER.WriteLine("<html>");
                FILE_WRITER.WriteLine("<head>");
                FILE_WRITER.WriteLine("<title>");
                FILE_WRITER.WriteLine("Page Not Found");
                FILE_WRITER.WriteLine("</title>");
                FILE_WRITER.WriteLine("<body>");
                FILE_WRITER.WriteLine("<h1 align=center>");
                FILE_WRITER.WriteLine("Page Not Found");
                FILE_WRITER.WriteLine("</h1>");
                FILE_WRITER.WriteLine("</body>");
                FILE_WRITER.WriteLine("</html>");
                FILE_WRITER.Close();
                FILE_STREAM.Close();
            }
            NetworkInterface networkInterface = NetworkInterface.GetAllNetworkInterfaces()[0];
            networkInterface.EnableStaticIP(CONFIG.IpAddress, CONFIG.SubnetMask, CONFIG.DefaultGateWay);
            Thread.Sleep(1000);
        }

        public TinyWS(ServerConfiguration Config, ServerCredentials Credentials, int ReceiveBufferSize, int SendBufferSize, string pages_folder)
        {
            SERVER_THREAD = null;
            LISTEN_SOCKET = null;
            ACCEPTED_SOCKET = null;
            IS_SERVER_RUNNING = false;
            STORAGE_PATH = pages_folder;
            RECEIVE_BUFFER = new byte[ReceiveBufferSize];
            SEND_BUFFER = new byte[SendBufferSize];
            SERVER_THREAD = new Thread(new ThreadStart(RunServer));
            CONFIG = Config;
            this.CREDENTIALS = Credentials;
            USE_AUTHENTICATION = true;

            if (!File.Exists(STORAGE_PATH + "\\index.html"))
            {
                FILE_STREAM = new FileStream(STORAGE_PATH + "\\index.html", FileMode.Create, FileAccess.Write);
                FILE_WRITER = new StreamWriter(FILE_STREAM);
                FILE_WRITER.WriteLine("<html>");
                FILE_WRITER.WriteLine("<head>");
                FILE_WRITER.WriteLine("<title>");
                FILE_WRITER.WriteLine("Index Page");
                FILE_WRITER.WriteLine("</title>");
                FILE_WRITER.WriteLine("<body>");
                FILE_WRITER.WriteLine("<h1 align=center>");
                FILE_WRITER.WriteLine("FILE LIST");
                FILE_WRITER.WriteLine("</h1>");
                FILE_WRITER.WriteLine("</body>");
                FILE_WRITER.WriteLine("</html>");
                FILE_WRITER.Close();
                FILE_STREAM.Close();
            }
            if (!File.Exists(STORAGE_PATH + "\\NotFound.html"))
            {
                FILE_STREAM = new FileStream(STORAGE_PATH + "\\NotFound.html", FileMode.Create, FileAccess.Write);
                FILE_WRITER = new StreamWriter(FILE_STREAM);
                FILE_WRITER.WriteLine("<html>");
                FILE_WRITER.WriteLine("<head>");
                FILE_WRITER.WriteLine("<title>");
                FILE_WRITER.WriteLine("Page Not Found");
                FILE_WRITER.WriteLine("</title>");
                FILE_WRITER.WriteLine("<body>");
                FILE_WRITER.WriteLine("<h1 align=center>");
                FILE_WRITER.WriteLine("Page Not Found");
                FILE_WRITER.WriteLine("</h1>");
                FILE_WRITER.WriteLine("</body>");
                FILE_WRITER.WriteLine("</html>");
                FILE_WRITER.Close();
                FILE_STREAM.Close();
            }
            NetworkInterface networkInterface = NetworkInterface.GetAllNetworkInterfaces()[0];
            networkInterface.EnableStaticIP(CONFIG.IpAddress, CONFIG.SubnetMask, CONFIG.DefaultGateWay);
            Thread.Sleep(1000);
        }

        public void Start()
        {
            SERVER_THREAD.Start();
        }

        public void Stop()
        {
            LISTEN_SOCKET.Close();
        }

        public void Send(string FileName)
        {
            string FILE_EXTENTION = GetFileExtention(FileName.ToLower());
            switch (FILE_EXTENTION)
            {
                case "gif":
                    FragmentateAndSend(FileName, FileType.GIF);
                    break;
                case "txt":
                    FragmentateAndSend(FileName, FileType.Html);
                    break;
                case "jpg":
                    FragmentateAndSend(FileName, FileType.JPEG);
                    break;
                case "jpeg":
                    FragmentateAndSend(FileName, FileType.JPEG);
                    break;
                case "htm":
                    FragmentateAndSend(FileName, FileType.Html);
                    break;
                case "html":
                    FragmentateAndSend(FileName, FileType.Html);
                    break;
                default:
                    FragmentateAndSend(FileName, FileType.Html);
                    break;
            }
        }

        public void Send(byte[] data)
        {
            int datalength = data.Length;
            int i = 0;
            while (datalength > 256)
            {
                ACCEPTED_SOCKET.Send(data, i, 256, SocketFlags.None);
                i += 256;
                datalength -= 256;
            }
            ACCEPTED_SOCKET.Send(data, i, datalength, SocketFlags.None);
        }

        public void SendNotFound()
        {
            FragmentateAndSend(STORAGE_PATH + "\\NotFound.Html", FileType.Html);
        }

        public void SendAJAX(string DataValues)
        {
            byte[] AJAXData;
            AJAXData = UTF8Encoding.UTF8.GetBytes(DataValues);
            ACCEPTED_SOCKET.Send(AJAXData, 0, AJAXData.Length, SocketFlags.None);
        }

        public string replace(string str, string unwanted, string replacement)
        {
            StringBuilder sb = new StringBuilder(str);
            sb.Replace(unwanted, replacement);
            return sb.ToString();
        }

    }
}




