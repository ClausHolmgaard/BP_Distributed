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
            listener = new Listener(AddLogEntry);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AddIps();

            btnListen.Focus();
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
            AddLogEntry(txtSend.Text);
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
            if(!portSuccess)
            {
                AddLogEntry("Error parsing port: " + txtPort.Text);
            }
            listener.StartListening(cmbIp.Text, port);
        }
    }
}
