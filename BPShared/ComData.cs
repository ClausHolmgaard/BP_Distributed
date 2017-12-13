using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace BPShared
{
    interface ISerializable
    {
        StatusCode status { get; set; }
        string name { get; set; }
        string message { get; set; }
        bool acceptingWork { get; set; }

        string GetXML();
    }

    interface IDeserializable
    {
        StatusCode status { get; set; }
        string name { get; set; }
        string message { get; set; }
        bool acceptingWork { get; set; }

        void FromXML(string xml);
    }

    public enum StatusCode
    {
        idle = 0,
        processing = 1,
        errorState = -1,
    }

    public class ComData : ISerializable, IDeserializable
    {
        public StatusCode status { get; set; }
        public string name { get; set; }
        public string message { get; set; }
        public bool acceptingWork { get; set; }

        public string GetXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            StringWriter sWriter = new StringWriter();
            serializer.Serialize(sWriter, this);

            return sWriter.ToString().Replace(Environment.NewLine, "");
        }

        public void FromXML(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            ComData comData = new ComData();
            StringReader sReader = new StringReader(xml);
            try
            {
                ComData tmpComData = (ComData)serializer.Deserialize(sReader);
                status = tmpComData.status;
                name = tmpComData.name;
                message = tmpComData.message;
                acceptingWork = tmpComData.acceptingWork;
            }
            catch (InvalidOperationException)
            {
                // Ikke XML
                Console.WriteLine("Server: Invalid XML : " + xml);
            }
        }
    }

    public static class HandleData
    {
        public delegate void NewMessageDelegate(string msg, string name);

        public static event NewMessageDelegate NewMessageEvent;

        public static void HandleComData(ComData comData)
        {
            string name = "NoName";
            if (comData.name != "" && comData.name != null)
            {
                name = comData.name;
            }

            if (comData.message != "")
            {
                NewMessageEvent(comData.message, name);

            }
        }

    }

}
