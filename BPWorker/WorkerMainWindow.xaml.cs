﻿using System;
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
        Client comm;                // Communication
        ComDataToServer comData;    // Communication data structure
        Thread doWork;

        public MainWindow()
        {
            InitializeComponent();

            comm = new Client();
            comm.ComDataReceivedEvent += DataReceived;  // New data received
            comm.ConnectionChangedEvent += UpdateUI;    // Update the UI
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] ips = Helpers.GetAllLocalIPv4();
            txtIp.Text = ips[0];
            txtPort.Text = "11002";

            txtName.Text = Environment.MachineName;
            txtThreads.Text = Environment.ProcessorCount.ToString();
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
            // Send chat data
            UpdateComm();
            if (txtSend.Text != "")
            {
                comData = new ComDataToServer();
                comData.status = StatusCode.idle;
                comData.name = txtName.Text;
                comData.message = txtSend.Text;
                comData.acceptingWork = chkAcceptWork.IsChecked == true;

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

        private void Check_Click()
        {
            // Get settings
            bool threadsSucces = Int32.TryParse(txtThreads.Text, out int threads);
            bool batchSuccess = Int32.TryParse(txtBatchSize.Text, out int batchSize);

            // Validate settings
            if(!threadsSucces)
            {
                MessageBox.Show("Threads must be a valid integer");
                return;
            }
            if(!batchSuccess)
            {
                MessageBox.Show("Batch size must be a valid integer");
                return;
            }

            UpdateComm();

            // save settings
            comm.threads = threads;
            comm.batchSize = batchSize;
        }

        private void chkAcceptWork_Checked(object sender, RoutedEventArgs e)
        {
            Check_Click();
        }

        private void chkAcceptWork_Unchecked(object sender, RoutedEventArgs e)
        {
            Check_Click();
        }

        // Add entry in log
        private void AddLog(string msg)
        {
            ListBoxItem itm = new ListBoxItem();
            itm.Content = msg;
            lstLog.Items.Add(itm);
        }

        // Add entry in log from other thread
        private void AddLogEntry(string msg)
        {
            // Much pretty, very work...
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => {
                    AddLog(msg);
                }));
        }

        // Update communication data
        private void UpdateComm()
        {
            comm.acceptingWork = chkAcceptWork.IsChecked == true;
            comm.name = txtName.Text;
        }

        // Update the UI
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

        // Connect
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

        // Handle received data
        private void DataReceived(ComData comData)
        {
            ComDataToClient c = (ComDataToClient)comData;
            if(comData.message != "" && comData.message != null)
            {
                AddLogEntry("[" + comData.name + "] " + comData.message);
            }

            if(c.filename != "" && c.filename != null && c.start != null && c.end != null)
            {
                GotWork(c.filename, c.start, c.end, c.lower, c.upper, c.numbers, c.symbols);
            }
        }

        private void BatchResult(List<string> result)
        {
            foreach (string s in result)
            {
                Console.WriteLine("RESULT: " + s);
            }
        }

        // Parameters for worker thread
        private class WorkParams
        {
            public string file;
            public Tuple<char[], char[]> batch;
            public bool lower;
            public bool upper;
            public bool numbers;
            public bool symbols;
        }

        // Reveiced work
        private void GotWork(string file, string start, string end, bool lower, bool upper, bool numbers, bool symbols)
        {
            Tuple<char[], char[]> b = new Tuple<char[], char[]>(start.ToCharArray(), end.ToCharArray());

            WorkParams wp = new WorkParams();
            wp.file = file;
            wp.batch = b;
            wp.lower = lower;
            wp.upper = upper;
            wp.numbers = numbers;
            wp.symbols = symbols;

            doWork = new Thread(new ParameterizedThreadStart(CompleteWorkThread));
            doWork.Start(wp);
        }

        // Thread for doing the work
        private void CompleteWorkThread(object infoObj)
        {
            comm.SendWorkAccepted();

            WorkParams p = (WorkParams)infoObj;

            bool passFound = false;
            BreakPass bp = new BreakPass(p.lower, p.upper, p.numbers, p.symbols, p.file);
            List<string> res = bp.CrackManagedExe(comm.threads, comm.batchSize, p.batch);
            foreach (string s in res)
            {
                comm.SendPassword(s);
                passFound = true;
            }
            comm.SendWorkCompleted(new string(p.batch.Item1), new string(p.batch.Item2), passFound);
        }

    }
}
