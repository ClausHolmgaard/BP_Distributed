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

    /* TCP communication is modified from
    https://scatteredcode.wordpress.com/2013/04/29/creating-a-single-threaded-multi-user-tcp-server-in-net/
    */

    public class NetworkClient
    {
        public delegate void MessageReceivedDelegate(NetworkClient client, ComData comData);
        public delegate void ClientDisconnectedDelegate(NetworkClient client);

        public event MessageReceivedDelegate MessageReceived;
        public event ClientDisconnectedDelegate ClientDisconnected;

        private TcpClient socket;
        private NetworkStream networkStream;
        private int id;

        public bool IsActive;
        public int Id;
        public string name;
        public TcpClient Socket;

        public NetworkClient(TcpClient clientSocket, int clientId)
        {
            socket = clientSocket;
            id = clientId;
        }

        private void MarkAsDisconnected()
        {
            IsActive = false;
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
                            Console.WriteLine("Server: Received: " + content);
                            ComData comData = new ComData();
                            comData.FromXML(content);

                            if(comData != null)
                            {
                                if(comData.name != null && comData.name != "")
                                {
                                    Console.WriteLine("Settings name: " + comData.name);
                                    name = comData.name;
                                }
                            }

                            MessageReceived(this, comData);
                        }
                    }

                    // If the tcp connection is ungracefully disconnected, it will throw an exception
                    catch (IOException)
                    {
                        Console.WriteLine("Server: Error handling message");
                        MarkAsDisconnected();
                        return;
                    }
                }
            }
        }

        public async Task SendLine(ComData comData)
        {
            if (!IsActive)
                return;

            try
            {
                var writer = new StreamWriter(networkStream);
                await writer.WriteLineAsync(comData.GetXML());
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
        public delegate void ClientsChangedDelegate();
        public delegate void ComDataReceviedDelegate(ComData cmData);

        public event ClientsChangedDelegate ClientsChangedEvent;
        public event ComDataReceviedDelegate ComDataReceivedEvent;

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

            ComDataReceivedEvent += HandleData.HandleComData;

            AddLog = log;
        }

        /*
        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
                }));
        }
        */

        public List<NetworkClient> GetClients()
        {
            return networkClients;
        }

        private void ProcessClientCommand(NetworkClient client, ComData comData)
        {
            ComDataReceivedEvent(comData);

            // Adding this to catch name changes, consider finding a better way to do it, so updates does not happen as often.
            ClientsChangedEvent();
        }

        private void ClientDisconnected(NetworkClient client)
        {
            
            client.IsActive = false;
            
            if (networkClients.Contains(client))
            {
                Console.WriteLine("Removing client " + client.Id);
                networkClients.Remove(client);
            }

            Console.WriteLine("Client " + client.Id + " disconnected");
            ClientsChangedEvent();
        }

        private void ClientConnected(TcpClient client, int clientNumber)
        {
            var netClient = new NetworkClient(client, clientNumber);
            netClient.MessageReceived += ProcessClientCommand;
            netClient.ClientDisconnected += ClientDisconnected;
            netClient.Id = clientNumber;

            // Save the Resulting task from ReceiveInput as a Task so we can check for any unhandled exceptions that may have occured
            KeyValuePair<Task, NetworkClient> kp = new KeyValuePair<Task, NetworkClient>(netClient.ReceiveInput(), netClient);
            networkClientReceiveInputTasks.Add(kp);

            networkClients.Add(netClient);
            Console.WriteLine("Client " + clientNumber + " Connected");
            Console.WriteLine("Server: Client connected.");
            ClientsChangedEvent();
        }

        private async Task ListenForClients()
        {
            var numClients = 0;

            Console.WriteLine("Listening for clients...");
            while (IsRunning)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                ClientConnected(tcpClient, numClients);
                numClients++;
            }

            Console.WriteLine("Stopping listener...");
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
                Console.WriteLine("Problem connecting. Are you already connected?");
            }
            IsRunning = true;

            clientListenTask = ListenForClients();
        }

        public string GetNameFromID(int id)
        {
            foreach(NetworkClient netClient in networkClients)
            {
                if(netClient.Id == id)
                {
                    if(netClient.name != "" && netClient.name != null)
                    {
                        return netClient.name;
                    }
                    else
                    {
                        return "NoName";
                    }
                }
            }
            return null;
        }

        public async void Send(ComData comData, int id=-1)
        {
            foreach (NetworkClient netClient in networkClients)
            {
                if (id != -1 && id == netClient.Id)
                {
                    Console.WriteLine("Sending message to client " + netClient.Id);
                    if (netClient.IsActive)
                    {
                        await netClient.SendLine(comData);
                    }
                }
                else if(id == -1)
                {
                    Console.WriteLine("Sending message to client " + netClient.Id);
                    if (netClient.IsActive)
                    {
                        await netClient.SendLine(comData);
                    }
                }
            }
        }
    }
}
