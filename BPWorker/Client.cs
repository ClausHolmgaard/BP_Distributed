using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BPWorker
{

    class Client
    {
        TcpClient tcpCon;
        private string ip;
        private int port;
        public Action<string> AddLog;
        Thread listenThread;
        bool isRunning = false;

        public Client(Action<string> log)
        {
            AddLog = log;
            tcpCon = new TcpClient();
        }

        public void connect(string remoteIp, int remotePort)
        {
            ip = remoteIp;
            port = remotePort;
            isRunning = true;

            AddLogEntry("Connecting to " + ip + ":" + port);

            try
            {
                if (!tcpCon.Connected)
                {
                    tcpCon.Connect(ip, port);
                    AddLogEntry("Connected to " + ip + ":" + port.ToString());
                }
                else
                {
                    AddLogEntry("Already connected");
                }
                
            }
            catch (SocketException)
            {
                AddLogEntry("Error connecting to " + ip + ":" + port.ToString());
                return;
            }

            listenThread = new Thread(listen);
            listenThread.Start();
        }

        public void stop()
        {
            isRunning = false;
            listenThread.Join();
        }

        private void listen()
        {
            while (isRunning)
            {
                NetworkStream nStream = tcpCon.GetStream();
                StreamReader nStreamReader = new StreamReader(nStream);

                string read = nStreamReader.ReadLine();
                Console.WriteLine("Worker: Received: " + read);
                AddLogEntry(read);
            }
        }

        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
                }));
        }

        public bool getConnected()
        {
            return tcpCon.Connected;
        }

        public void send(string msg)
        {
            if (tcpCon.Connected)
            {
                //AddLogEntry("Sending: " + msg);
                NetworkStream networkStream = tcpCon.GetStream();

                byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(msg + '\n');
                networkStream.Write(bytesToSend, 0, bytesToSend.Length);
                Console.WriteLine("Worker: Message sent");
            }
            else
            {
                AddLogEntry("Not connected");
            }
        }

    }

}
