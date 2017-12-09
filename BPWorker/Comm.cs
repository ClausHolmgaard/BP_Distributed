using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BPWorker
{
    class Comm
    {
        private string ip;
        private int port;
        public Action<string> AddLog;

        public Comm(string remoteIp, int remotePort, Action<string> log)
        {
            ip = remoteIp;
            port = remotePort;
            AddLog = log;

            connect();
            
        }

        private void connect()
        {
            TcpClient tcpCon = new TcpClient();
            try
            {
                tcpCon.Connect(ip, port);
            }
            catch (System.Net.Sockets.SocketException)
            {
                AddLogEntry("Error connecting to " + ip + ":" + port.ToString());
                return;
            }

            AddLogEntry("Connected to " + ip + ":" + port.ToString());
        }

        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
                }));
        }

    }
}
