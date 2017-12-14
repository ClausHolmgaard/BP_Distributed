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

    // Class for handling a client connection
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
        public bool acceptingWork { get; set; }
        public StatusCode status { get; set; }
        public TcpClient Socket;

        // constructor
        public NetworkClient(TcpClient clientSocket, int clientId)
        {
            socket = clientSocket;
            id = clientId;
        }

        // Client needs to be disconnected
        private void MarkAsDisconnected()
        {
            IsActive = false;
            ClientDisconnected(this);
        }

        // Input received
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

                        // We've got a message
                        if (MessageReceived != null)
                        {
                            Console.WriteLine("Server: Received: " + content);
                            ComDataToServer comData = new ComDataToServer();
                            comData.FromXML(content);

                            if(comData != null)
                            {
                                if(comData.name != null && comData.name != "")
                                {
                                    Console.WriteLine("Settings name: " + comData.name);
                                    name = comData.name;
                                }

                                // Save accepting work status
                                acceptingWork = comData.acceptingWork;
                                status = comData.status;
                            }

                            // Handle the received data
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

        // Send a ComData object
        public async Task SendLine(ComData comData)
        {
            // Only if active
            if (!IsActive)
                return;

            // Send the data
            try
            {
                var writer = new StreamWriter(networkStream);
                // Send the xml string
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

    // Handling server communication
    public class Listener
    {
        public delegate void ClientsChangedDelegate();
        public delegate void NewMessageDelegate(string msg, string name);
        public delegate void passwordFoundDelegate(string password);

        public event ClientsChangedDelegate ClientsChangedEvent;
        public event NewMessageDelegate NewMessageEvent;
        public event passwordFoundDelegate PasswordFoundEvent;

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

        // Constructor
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

            // Log for printing status in UI
            AddLog = log;
        }

        // Return all clients
        public List<NetworkClient> GetClients()
        {
            return networkClients;
        }

        // Process received data
        private void ProcessClientCommand(NetworkClient client, ComData comData)
        {
            // Handle the data
            HandleComData((ComDataToServer)comData);

            // Adding this to catch name changes, consider finding a better way to do it, so updates does not happen as often.
            ClientsChangedEvent();
        }

        // Handle client disconnects
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

        // A client connected
        private void ClientConnected(TcpClient client, int clientNumber)
        {
            var netClient = new NetworkClient(client, clientNumber);
            netClient.MessageReceived += ProcessClientCommand;  // Handle messages received
            netClient.ClientDisconnected += ClientDisconnected; // Handle client disconnects
            netClient.Id = clientNumber;

            // Save the Resulting task from ReceiveInput as a Task so we can check for any unhandled exceptions that may have occured
            KeyValuePair<Task, NetworkClient> kp = new KeyValuePair<Task, NetworkClient>(netClient.ReceiveInput(), netClient);
            networkClientReceiveInputTasks.Add(kp);

            networkClients.Add(netClient);
            Console.WriteLine("Client " + clientNumber + " Connected");
            Console.WriteLine("Server: Client connected.");

            // Update the UI
            ClientsChangedEvent();
        }

        // Listen for new connections
        private async Task ListenForClients()
        {
            var numClients = 0;

            Console.WriteLine("Listening for clients...");
            while (IsRunning)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                // New client connected
                ClientConnected(tcpClient, numClients);
                numClients++;
            }

            Console.WriteLine("Stopping listener...");
            listener.Stop();
        }

        // Return running state
        public bool getRunning()
        {
            return IsRunning;
        }

        // Start listening
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

        // Get a clients name from the ID
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

        // Handle received data
        public void HandleComData(ComDataToServer comData)
        {
            string name = "NoName";
            if (comData.name != "" && comData.name != null)
            {
                name = comData.name;
            }

            // Chat messages
            if (comData.message != "" && comData.message != null)
            {
                NewMessageEvent(comData.message, name);
            }

            // Password message
            if(comData.password != "" && comData.password != null)
            {
                PasswordFoundEvent(comData.password);
            }
        }

        // Send ComData object
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

        // Send workorder to a client
        public void SendWorkOrder(int clientId, string file, char[] start, char[] end, bool lower, bool upper, bool numbers, bool symbols)
        {
            ComDataToClient comData = new ComDataToClient();
            comData.message = "";
            comData.filename = file;
            comData.start = new string(start);
            comData.end = new string(end);
            comData.lower = lower;
            comData.upper = upper;
            comData.numbers = numbers;
            comData.symbols = symbols;

            Send(comData, clientId);
        }
    }
}
