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
        private Helpers hlp;
        private Listener listener;

        public MainWindow()
        {
            InitializeComponent();

            hlp = new Helpers();
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
                lstClients.Items.Add(itm);
            }

        }

        private void AddIps()
        {
            ComboBoxItem cmbAll = new ComboBoxItem();
            cmbAll.Content = "All";
            cmbIp.Items.Add(cmbAll);

            string[] ips = hlp.GetAllLocalIPv4();
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

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            listener.SendAll(txtSend.Text);
            txtSend.Text = "";

            UpdateClients();
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
            listener.NewClientEvent += UpdateClients;
            listener.Run();
        }

        private void btnSendToSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach(ListBoxItem item in lstClients.SelectedItems)
            {
                string selectedItem = item.Content.ToString();
                Int32.TryParse(selectedItem.Substring(selectedItem.LastIndexOf("ID: ")).Replace("ID: ", ""), out int clientID);
                Console.WriteLine("Sending message to client " + clientID);

                listener.SendToClient(txtSend.Text, clientID);
            }

            string selected = lstClients.SelectedItems.ToString();
            
        }
    }
}