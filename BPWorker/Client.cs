using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using BPShared;

namespace BPWorker
{
    class Client
    {
        public delegate void ComDataReceivedDelegate(ComData comData);
        public delegate void ConnectionChangedDelegate();

        public event ComDataReceivedDelegate ComDataReceivedEvent;
        public event ConnectionChangedDelegate ConnectionChangedEvent;

        TcpClient tcpCon;
        private string ip;
        private int port;
        public bool acceptingWork { get; set; }
        public string name { get; set; }
        public bool isConnected { get; private set; }

        Thread listenThread;
        Thread keepAliveThread;

        public void connect(string remoteIp, int remotePort)
        {
            tcpCon = new TcpClient();
            ip = remoteIp;
            port = remotePort;

            Console.WriteLine("Worker: Connecting to " + ip + ":" + port);

            try
            {
                if (!tcpCon.Connected)
                {
                    tcpCon.Connect(ip, port);
                    Console.WriteLine("Worker: Connected to " + ip + ":" + port.ToString());
                }
                else
                {
                    Console.WriteLine("Worker: Already connected");
                }
                
            }
            catch (SocketException)
            {
                Console.WriteLine("Error connecting to " + ip + ":" + port.ToString());
                return;
            }

            isConnected = true;

            listenThread = new Thread(listen);
            listenThread.SetApartmentState(ApartmentState.STA);
            listenThread.Start();

            keepAliveThread = new Thread(KeepAlive);
            keepAliveThread.Start();

            ConnectionChangedEvent();
        }

        public void stop()
        {
            if (tcpCon != null)
            {
                tcpCon.GetStream().Close();
                tcpCon.Close();
            }

            isConnected = false;
            ConnectionChangedEvent();
        }

        private void listen()
        {
            while (isConnected)
            {
                NetworkStream nStream = tcpCon.GetStream();
                StreamReader nStreamReader = new StreamReader(nStream);

                string read;
                try
                {
                    read = nStreamReader.ReadLine();
                }
                catch (IOException)
                {
                    Console.WriteLine("Socket read interruptet, assuming connection is beeing closed");
                    return;
                }
                
                Console.WriteLine("Worker: Received: " + read);

                ComDataToClient comData = new ComDataToClient();
                comData.FromXML(read);

                ComDataReceivedEvent(comData);
            }

            ConnectionChangedEvent();
        }

        public void send(ComData comData)
        {
            if (tcpCon.Connected)
            {
                NetworkStream networkStream = tcpCon.GetStream();

                byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(comData.GetXML() + '\n');
                Console.WriteLine("Worker: Sending: " + comData.GetXML());
                networkStream.Write(bytesToSend, 0, bytesToSend.Length);
            }
            else
            {
                Console.WriteLine("Not connected");
            }
        }
        
        private void KeepAlive()
        {
            while (isConnected)
            {
                ComDataToServer comData = new ComDataToServer();
                comData.acceptingWork = acceptingWork;
                comData.name = name;
                comData.status = StatusCode.idle;

                Console.WriteLine("Worker: Sending keepalive");
                send(comData);

                Thread.Sleep(1000);
            }
        }

        public void SendWorkAccepted()
        {
            if(isConnected)
            {
                acceptingWork = false;
                ComDataToServer comData = new ComDataToServer();
                comData.acceptingWork = false;
                comData.name = name;
                comData.status = StatusCode.processing;
            }
        }

        public void SendWorkCompleted()
        {
            if (isConnected)
            {
                acceptingWork = true;
                ComDataToServer comData = new ComDataToServer();
                comData.acceptingWork = true;
                comData.name = name;
                comData.status = StatusCode.idle;
            }
        }

        public void SendPassword(string pass)
        {
            if (isConnected)
            {
                ComDataToServer comData = new ComDataToServer();
                comData.name = name;
                comData.password = pass;
            }
        }
    }

}
