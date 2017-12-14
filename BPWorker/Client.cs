using System;
using System.Collections.Concurrent;
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
        public StatusCode status { get; set; }
        public string name { get; set; }
        public int threads { get; set; }
        public int batchSize { get; set; }
        public bool isConnected { get; private set; }

        Thread listenThread;
        Thread sendThread;
        Thread keepAliveThread;

        // Thread safe queue for sending tcp messages
        ConcurrentQueue<ComDataToServer> tcpQueue;

        // Connect to a server
        public void connect(string remoteIp, int remotePort)
        {
            tcpCon = new TcpClient();
            ip = remoteIp;
            port = remotePort;

            tcpQueue = new ConcurrentQueue<ComDataToServer>();

            Console.WriteLine("Worker: Connecting to " + ip + ":" + port);

            // connect
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

            // Thread for listening for messages
            listenThread = new Thread(listen);
            listenThread.SetApartmentState(ApartmentState.STA);
            listenThread.Start();

            // Thread for sending messages
            sendThread = new Thread(Send);
            sendThread.Start();

            // Thread for sending keepalives...
            keepAliveThread = new Thread(KeepAlive);
            keepAliveThread.Start();

            // Notify about change in connection
            ConnectionChangedEvent();
        }

        // Stop communication
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

        // Listen for messages
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

                // Handle new message
                ComDataReceivedEvent(comData);
            }

            ConnectionChangedEvent();
        }

        // Thread for sending message from queue list
        public void Send()
        {
            while (isConnected)
            {
                if (tcpCon.Connected)
                {
                    ComDataToServer comData;
                    if(tcpQueue.TryDequeue(out comData))
                    {
                        NetworkStream networkStream = tcpCon.GetStream();

                        byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(comData.GetXML() + '\n');
                        Console.WriteLine("Worker: Sending: " + comData.GetXML());
                        networkStream.Write(bytesToSend, 0, bytesToSend.Length);
                    }
                }
                else
                {
                    Console.WriteLine("Not connected");
                }
            }
        }

        // Add a message to the queue
        public void send(ComDataToServer comData)
        {
            tcpQueue.Enqueue(comData);
        } 

        // Thread for sending keepalives
        private void KeepAlive()
        {
            while (isConnected)
            {
                ComDataToServer comData = new ComDataToServer();
                comData.acceptingWork = acceptingWork;
                comData.name = name;
                comData.status = status;

                Console.WriteLine("Worker: Sending keepalive");
                tcpQueue.Enqueue(comData);

                Thread.Sleep(1000);
            }
        }

        // Send a message that work has been accepted
        public void SendWorkAccepted()
        {
            if(isConnected)
            {
                status = StatusCode.processing;
                ComDataToServer comData = new ComDataToServer();
                comData.acceptingWork = acceptingWork;
                comData.name = name;
                comData.status = status;

                tcpQueue.Enqueue(comData);
            }
        }

        // Send a message that work has been completed
        public void SendWorkCompleted(string start, string end, bool passFound)
        {
            if (isConnected)
            {
                status = StatusCode.idle;
                ComDataToServer comData = new ComDataToServer();
                comData.acceptingWork = acceptingWork;
                comData.name = name;
                comData.status = status;
                comData.isWorkMessage = true;

                WorkStatus w = new WorkStatus();
                w.start = start;
                w.end = end;
                w.passFound = passFound;
                

                comData.workStatus = w;

                tcpQueue.Enqueue(comData);
            }
        }

        // Send a message that a password has been found
        public void SendPassword(string pass)
        {
            if (isConnected)
            {
                ComDataToServer comData = new ComDataToServer();
                comData.name = name;
                comData.password = pass;
                comData.acceptingWork = acceptingWork;
                comData.status = status;

                tcpQueue.Enqueue(comData);
            }
        }
    }

}
