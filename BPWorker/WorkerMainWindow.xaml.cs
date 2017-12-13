using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using System.Xml.Serialization;
using System.IO;
using System.Threading;

namespace BPWorker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Client comm;
        ComData comData;

        public MainWindow()
        {
            InitializeComponent();

            //hlp = new Helpers();
            comm = new Client();
            comm.ComDataReceivedEvent += DataReceived;
            comm.ConnectionChangedEvent += UpdateUI;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] ips = Helpers.GetAllLocalIPv4();
            txtIp.Text = ips[0];
            txtPort.Text = "11002";

            txtName.Text = Environment.MachineName;

            UpdateUI();
            UpdateComm();
            txtIp.Focus();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            disconnect();
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            UpdateComm();
            if (txtSend.Text != "")
            {
                comData = new ComData();
                comData.status = StatusCode.idle;
                comData.name = txtName.Text;
                comData.message = txtSend.Text;

                comm.send(comData);

                txtSend.Text = "";
            }
        }

        private void txtSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Enter))
            {
                btnSend_Click(this, new RoutedEventArgs());
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            connect();
        }

        private void txtIp_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Enter))
            {
                btnConnect_Click(this, new RoutedEventArgs());
            }
        }

        private void txtPort_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Enter))
            {
                btnConnect_Click(this, new RoutedEventArgs());
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            disconnect();
        }

        private void chkAcceptWork_Checked(object sender, RoutedEventArgs e)
        {
            UpdateComm();
        }

        private void chkAcceptWork_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateComm();
        }

        private void AddLog(string msg)
        {
            ListBoxItem itm = new ListBoxItem();
            itm.Content = msg;
            lstLog.Items.Add(itm);
        }

        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
                }));
        }

        private void UpdateComm()
        {
            comm.acceptingWork = chkAcceptWork.IsChecked == true;
            comm.name = txtName.Text;
        }

        private void UpdateUI()
        {
            if (comm.isConnected)
            {
                btnConnect.IsEnabled = false;
                txtIp.IsEnabled = false;
                txtPort.IsEnabled = false;

                btnDisconnect.IsEnabled = true;
                txtSend.IsEnabled = true;
                btnSend.IsEnabled = true;
            }
            else
            {
                btnConnect.IsEnabled = true;
                txtIp.IsEnabled = true;
                txtPort.IsEnabled = true;

                btnDisconnect.IsEnabled = false;
                txtSend.IsEnabled = false;
                btnSend.IsEnabled = false;
            }
        }

        private void connect()
        {
            bool portSuccess = Int32.TryParse(txtPort.Text, out int port);
            if (!portSuccess)
            {
                AddLogEntry("Error parsing port...");
                return;
            }

            comm.connect(txtIp.Text, port);
            
        }

        private void disconnect()
        {
            comm.stop();
        }

        private void NewChatEntry(string msg)
        {
            AddLogEntry(msg);
        }

        private void DataReceived(ComData comData)
        {
            if(comData.message != "" && comData.message != null)
            {
                AddLogEntry("[" + comData.name + "] " + comData.message);
            }
        }

        private void btnTestWork_Click(object sender, RoutedEventArgs e)
        {
            string file = @"C:\Temp\EncryptedFile_6c.exe";
            //AppDomain encryptedFile = AppDomain.CreateDomain("New Appdomain");

            //char[] start = { 'a' };
            //char[] end = { 'z', 'z', 'z', 'z' };
            //List<Tuple<Char[], char[]>> lst = BreakPass.Split(100000, start, end, true, false, false, false);

            BreakPass bp = new BreakPass(1, 5, true, true, false, false, file);

            //bp.run(2, file);
            bp.CrackManagedExe(4, 10000000);
            //bp.CrackAnyExe(2);


        }
    }
}
