using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using BPShared;

namespace BPCoordinator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Listener listener;
        HandleWorkers hw;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AddIps();

            btnListen.Focus();
        }

        private void UpdateClients()
        {
            List<NetworkClient> networkClients = listener.GetClients();
            lstClients.Items.Clear();

            foreach (NetworkClient client in networkClients)
            {
                string clientInfo = "";
                if (client.name == "")
                {
                    clientInfo += "NoName";
                }
                else
                {
                    clientInfo += client.name;
                }
                clientInfo += " - ID: " + client.Id;
                ListBoxItem itm = new ListBoxItem();
                itm.Content = clientInfo;
                if (client.acceptingWork)
                {
                    itm.Background = Brushes.Green;
                }
                if(!client.acceptingWork)
                {
                    itm.Background = Brushes.Red;
                }

                lstClients.Items.Add(itm);
            }
        }

        private List<NetworkClient> ClientsForWorkerHandling()
        {
            if (listener != null)
            {
                return listener.GetClients();
            }
            return null;
        }

        private void AddIps()
        {
            ComboBoxItem cmbAll = new ComboBoxItem();
            cmbAll.Content = "All";
            cmbIp.Items.Add(cmbAll);

            string[] ips = Helpers.GetAllLocalIPv4();
            foreach (string ip in ips)
            {
                ComboBoxItem cmbItem = new ComboBoxItem();
                cmbItem.Content = ip;
                cmbIp.Items.Add(cmbItem);
            }

            cmbIp.SelectedIndex = 0;
        }

        private void AddLogEntry(string msg)
        {
            ListBoxItem itm = new ListBoxItem();
            itm.Content = msg;
            lstLog.Items.Add(itm);
        }

        private void AddChatEntry(string message, string name)
        {
            if (message != "" && message != null)
            {
                string msg = "[" + name + "] " + message;
                AddLogEntry(msg);
            }

            ComData cd = new ComDataToClient();
            cd.message = message;
            cd.name = name;

            listener.Send(cd);
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            AddChatEntry(txtSend.Text, "Server");
            txtSend.Text = "";
        }

        private void txtSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Enter))
            {
                btnSend_Click(this, new RoutedEventArgs());
            }
        }

        private void btnListen_Click(object sender, RoutedEventArgs e)
        {
            bool portSuccess = Int32.TryParse(txtPort.Text, out int port);
            if (!portSuccess)
            {
                AddLogEntry("Error parsing port: " + txtPort.Text);
            }
            listener = new Listener(cmbIp.Text, port, AddLogEntry);
            listener.ClientsChangedEvent += UpdateClients;

            // Echo messages to clients
            HandleData.NewMessageEvent += AddChatEntry;

            listener.Run();
        }

        private void btnSendToSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach(ListBoxItem item in lstClients.SelectedItems)
            {
                string selectedItem = item.Content.ToString();
                Int32.TryParse(selectedItem.Substring(selectedItem.LastIndexOf("ID: ")).Replace("ID: ", ""), out int clientID);

                string clientName = listener.GetNameFromID(clientID);
                string msg = "[Server -> " + clientName + "] " + txtSend.Text;

                ComData cd = new ComDataToClient();
                cd.message = msg;

                if (msg != "" && msg != null)
                {
                    AddLogEntry(msg);
                }
                listener.Send(cd, clientID);
            }

            string selected = lstClients.SelectedItems.ToString();
        }

        private void btnProcess_Click(object sender, RoutedEventArgs e)
        {
            bool lower = chkLowerCase.IsChecked == true;
            bool upper = chkUpperCase.IsChecked == true;
            bool numbers = chkNumbers.IsChecked == true;
            bool symbols = chkSymbols.IsChecked == true;
            bool minSuccess = Int32.TryParse(txtMinLength.Text, out int minLength);
            bool maxSuccess = Int32.TryParse(txtMaxLength.Text, out int maxLength);
            bool batchSuccess = Int32.TryParse(txtBatchSize.Text, out int batchSize);

            if(!minSuccess || !maxSuccess)
            {
                MessageBox.Show("Both min and max length must be valid integers.");
                return;
            }
            if(minLength > maxLength)
            {
                MessageBox.Show("Max length must be greater than or equal to min length");
                return;
            }
            if(!batchSuccess)
            {
                MessageBox.Show("Batch size must be a valid integer");
                return;
            }

            hw = new HandleWorkers(minLength, maxLength, batchSize, txtFilename.Text, lower, upper, numbers, symbols);
            hw.BatchUpdate += BachUpdate;
            hw.GetClients += ClientsForWorkerHandling;
            hw.SendClientWork += listener.SendWorkOrder;
        }

        private void BachUpdate()
        {
            this.Dispatcher.Invoke(() =>
            {
                lstBatches.Items.Clear();
            });


            foreach (Tuple<char[], char[]> t in hw.GetBatchesNotProcessed())
            {
                string start = new string(t.Item1);
                string end = new string(t.Item2);

                this.Dispatcher.Invoke(() =>
                {
                    ListBoxItem itm = new ListBoxItem();
                    itm.Content = start + " -> " + end;
                    lstBatches.Items.Add(itm);
                });
            }
        }
    }
}