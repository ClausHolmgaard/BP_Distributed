using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BPCoordinator
{
    class Listener
    {
        private Thread listenThread;
        private bool isListening = false;
        private string ip;
        private int port;
        public Action<string> AddLog;
        List<Task> connectionTasks;
        object _lock;

        public Listener(Action<string> log)
        {
            AddLog = log;
            connectionTasks = new List<Task>();
            _lock = new Object();
        }

        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
            }));
        }

        public void StartListening(string localIp, int localPort)
        {
            
            ip = localIp;
            port = localPort;

            /*
            listenThread = new Thread(Listening);
            listenThread.SetApartmentState(ApartmentState.STA);
            listenThread.Start();
            */

          

        }

       

   
        private void Listening()
        {
            AddLogEntry("Starting Listener on " + ip + ":" + port.ToString() + "...");

            IPAddress ipAd;
            if (ip == "All")
            {
                ipAd = IPAddress.Any;
            }
            else
            {
                ipAd = IPAddress.Parse(ip);
            }
            TcpListener tcpListener = new TcpListener(ipAd, port);

            try
            {
                tcpListener.Start();
            }
            catch (SocketException)
            {
                AddLogEntry("Error starting Listener on " + ip + ":" + port.ToString());
            }

            AddLogEntry("Listener running on " + ip + ":" + port.ToString());

            while (isListening)
            {
                Socket s = tcpListener.AcceptSocket();
                AddLogEntry("Connection from " + s.RemoteEndPoint);
            }
        }

        public void send(string msg)
        {

        }
    }
}
