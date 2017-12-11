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

namespace BPWorker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Helpers hlp;
        Client comm;
        ComData comData;

        public MainWindow()
        {
            InitializeComponent();

            hlp = new Helpers();
            comm = new Client(AddLogEntry);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string[] ips = hlp.GetAllLocalIPv4();
            txtIp.Text = ips[0];
            txtPort.Text = "11002";

            txtIp.Focus();
        }

        private void AddLogEntry(string msg)
        {
            ListBoxItem itm = new ListBoxItem();
            itm.Content = msg;
            lstLog.Items.Add(itm);
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Connected: " + comm.getConnected());
            if (txtSend.Text != "")
            {
                comm.send(txtSend.Text);
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
            bool portSuccess = Int32.TryParse(txtPort.Text, out int port);
            if(!portSuccess)
            {
                AddLogEntry("Error parsing port...");
                return;
            }

            comm.connect(txtIp.Text, port);

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

        private void btnSendXML_Click(object sender, RoutedEventArgs e)
        {
            comData = new ComData();
            comData.error = ErrorCode.messageOK;
            comData.status = StatusCode.idle;
            comData.name = "Test";
            comData.id = 1;

            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            StringWriter sWriter = new StringWriter();
            serializer.Serialize(sWriter, comData);

            string myXML = sWriter.ToString();
            Console.WriteLine("Sending XML:\n" + myXML);
            comm.send(myXML.Replace(Environment.NewLine, ""));

        }
    }
}
