using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using BPShared;
using System.Xml.Serialization;

namespace BPCoordinator
{

    /*
    https://scatteredcode.wordpress.com/2013/04/29/creating-a-single-threaded-multi-user-tcp-server-in-net/
    */
    
    public delegate void MessageReceivedDelegate(NetworkClient client, string message);
    public delegate void ClientDisconnectedDelegate(NetworkClient client);

    public class NetworkClient
    {
        private TcpClient socket;
        private NetworkStream networkStream;
        private int id;

        public bool IsActive;
        public int Id;
        public TcpClient Socket;

        public event MessageReceivedDelegate MessageReceived;
        public event ClientDisconnectedDelegate ClientDisconnected;

        public NetworkClient(TcpClient clientSocket, int clientId)
        {
            socket = clientSocket;
            id = clientId;
        }

        private void MarkAsDisconnected()
        {
            IsActive = false;
            if (ClientDisconnected != null)
                ClientDisconnected(this);
        }

        public async Task ReceiveInput()
        {
            IsActive = true;
            networkStream = socket.GetStream();

            using (var reader = new StreamReader(networkStream))
            {
                while (IsActive)
                {
                    try
                    {
                        Console.WriteLine("Server: Something received...");
                        var content = await reader.ReadLineAsync();

                        // If content is null, that means the connection has been gracefully disconnected
                        if (content == null)
                        {
                            Console.WriteLine("Server: Marking client as disconnected.");
                            MarkAsDisconnected();
                            return;
                        }

                        if (MessageReceived != null)
                        {
                            Console.WriteLine("Server: Message received...");
                            MessageReceived(this, content);
                        }
                    }

                    // If the tcp connection is ungracefully disconnected, it will throw an exception
                    catch (IOException)
                    {
                        Console.WriteLine("Error handling message");
                        MarkAsDisconnected();
                        return;
                    }
                }
            }
        }

        public async Task SendLine(string line)
        {
            if (!IsActive)
                return;

            try
            {
                var writer = new StreamWriter(networkStream);
                await writer.WriteLineAsync(line);
                writer.Flush();
            }
            catch (IOException)
            {
                // socket closed
                Console.WriteLine("Server: Error Sending, marking as disconnected.");
                MarkAsDisconnected();
            }
        }
    }

    class Listener
    {
        private TcpListener listener;
        private List<NetworkClient> networkClients;
        private List<KeyValuePair<Task, NetworkClient>> networkClientReceiveInputTasks; 
        private Task clientListenTask;
        public Action<string> AddLog;

        public bool IsRunning { get; private set; }

        public Exception ClientListenTaskException
        {
            get { return clientListenTask.Exception; }
        }

        public Listener(string ip, int port, Action<string> log)
        {
            IPAddress ipAd;
            if (ip == "All")
            {
                ipAd = IPAddress.Any;
            }
            else
            {
                ipAd = IPAddress.Parse(ip);
            }

            listener = new TcpListener(ipAd, port);
            networkClients = new List<NetworkClient>();
            networkClientReceiveInputTasks = new List<KeyValuePair<Task, NetworkClient>>();

            AddLog = log;
        }

        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
                }));
        }

        private async void ProcessClientCommand(NetworkClient client, string command)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            ComData comData = new ComData();
            StringReader sReader = new StringReader(command);
            try
            {
                comData = (ComData)serializer.Deserialize(sReader);
                AddLogEntry("Object received:");
                AddLogEntry("ID: " + comData.id);
                AddLogEntry("Name: " + comData.name);
                AddLogEntry("Error: " + (int)comData.error);
                AddLogEntry("Status: " + (int)comData.status);

            }
            catch(InvalidOperationException)
            {
                // Ikke XML
                AddLogEntry("Client " + client.Id + " wrote: " + command);
                foreach (var netClient in networkClients)
                    if (netClient.IsActive)
                        await netClient.SendLine(command);
            }

        }

        private void ClientDisconnected(NetworkClient client)
        {
            client.IsActive = false;
            client.Socket.Close();

            if (networkClients.Contains(client))
            {
                Console.WriteLine("Removing client " + client.Id);
                networkClients.Remove(client);
            }

            AddLogEntry("Client " + client.Id + " disconnected");
        }

        private void ClientConnected(TcpClient client, int clientNumber)
        {
            var netClient = new NetworkClient(client, clientNumber);
            netClient.MessageReceived += ProcessClientCommand;
            netClient.ClientDisconnected += ClientDisconnected;

            // Save the Resulting task from ReceiveInput as a Task so we can check for any unhandled exceptions that may have occured
            KeyValuePair<Task, NetworkClient> kp = new KeyValuePair<Task, NetworkClient>(netClient.ReceiveInput(), netClient);
            //kp.Key.Start();
            networkClientReceiveInputTasks.Add(kp);

            networkClients.Add(netClient);
            AddLogEntry("Client " + clientNumber + " Connected");
            Console.WriteLine("Server: Client connected.");
        }

        private async Task ListenForClients()
        {
            var numClients = 0;

            AddLogEntry("Listening for clients...");
            while (IsRunning)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                ClientConnected(tcpClient, numClients);
                numClients++;
            }

            AddLogEntry("Stopping listener...");
            listener.Stop();
        }

        public bool getRunning()
        {
            return IsRunning;
        }

        public void Run()
        {
            try
            {
                listener.Start();
            }
            catch(SocketException)
            {
                AddLogEntry("Problem connecting. Are you already connected?");
            }
            IsRunning = true;

            clientListenTask = ListenForClients();
        }

        public async void Send(string message)
        {
            string serverMessage = "Server says: " + message;
            AddLogEntry(serverMessage);

            Console.WriteLine("Clients: " + networkClients.Count);
            foreach (NetworkClient netClient in networkClients)
            {
                Console.WriteLine("Sending message to client " + netClient.Id);
                if (netClient.IsActive)
                {
                    await netClient.SendLine(serverMessage + '\n');
                }
            }
        }
    }
    
}
