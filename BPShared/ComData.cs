using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace BPShared
{
    interface ISerializable
    {
        string GetXML();
    }

    interface IDeserializable
    {
        void FromXML(string xml);
    }

    public enum StatusCode
    {
        idle = 0,
        processing = 1,
        errorState = -1,
    }

    [Serializable]
    public struct WorkStatus
    {
        public string start;
        public string end;
        public bool passFound;
    }

    // Our com object. Must be serializable and deserializeable
    [XmlInclude(typeof(ComDataToClient))]
    [XmlInclude(typeof(ComDataToServer))]
    [XmlInclude(typeof(WorkStatus))]
    [Serializable]
    public abstract class ComData : ISerializable, IDeserializable
    {
        public string message { get; set; }
        public string name { get; set; }

        protected int[] key = { 100, 500, 32 };
        protected int[] salt = { 32, 34, 54, 12 };

        // Convert to XML string
        public string GetXML()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            StringWriter sWriter = new StringWriter();
            serializer.Serialize(sWriter, this);

            string clearText = sWriter.ToString().Replace(Environment.NewLine, "");

            return encrypt(clearText, key, salt);
            //return sWriter.ToString().Replace(Environment.NewLine, "");
        }

        protected string encrypt(string clear, int[] key, int[] salt)
        {
            int index = 0;
            string encryptedString = "";
            char[] valids = Helpers.getValidEncryptChars();

            foreach (char c in clear)
            {
                if (Array.IndexOf(valids, c) > -1) {
                    int intKey = key[index % key.Length];
                    int intSalt = salt[index % salt.Length];
                    int moveKey = intKey + intSalt;
                    index++;

                    int newKey = Array.IndexOf(valids, c) + moveKey;
                    char newChar = valids[newKey % valids.Length];
                    
                    encryptedString += newChar.ToString();
                }
                else
                {
                    encryptedString += c.ToString();
                }
            }

            return encryptedString;
        }

        protected string decrypt(string cipher, int[] key, int[] salt)
        {
            string clearString = "";
            char[] valids = Helpers.getValidEncryptChars();

            int index = 0;
            foreach(char c in cipher)
            {
                if (Array.IndexOf(valids, c) > -1) {
                    int intKey = key[index % key.Length];
                    int intSalt = salt[index % salt.Length];
                    int moveKey = intKey + intSalt;
                    index++;
                    moveKey = moveKey % valids.Length;

                    int newKey = Array.IndexOf(valids, c) - moveKey;

                    char newChar;
                    if(newKey >= 0)
                    {
                        newChar = valids[newKey % valids.Length];
                    }
                    else
                    {
                        newChar = valids[valids.Length - Math.Abs(newKey)];
                    }

                    clearString += newChar.ToString();
                }
                else
                {
                    clearString += c.ToString();
                }
            }

            return clearString;
        }

        // Must implement a method to get object from XML string
        public abstract void FromXML(string xml);
    }

    [Serializable]
    public class ComDataToClient : ComData
    {
        // Filename and batch to try on file
        public string filename { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public bool lower { get; set; }
        public bool upper { get; set; }
        public bool symbols { get; set; }
        public bool numbers { get; set; }

        // Override abstract
        public override void FromXML(string xml)
        {
            string clearXml = decrypt(xml, key, salt);
            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            ComData comData = new ComDataToClient();
            
            try
            {
                StringReader sReader = new StringReader(clearXml);
                ComDataToClient tmpComData = (ComDataToClient)serializer.Deserialize(sReader);
                message = tmpComData.message;
                name = tmpComData.name;
                filename = tmpComData.filename;
                start = tmpComData.start;
                end = tmpComData.end;
                lower = tmpComData.lower;
                upper = tmpComData.upper;
                symbols = tmpComData.symbols;
                numbers = tmpComData.numbers;
            }
            catch (InvalidOperationException)
            {
                // Not XML
                Console.WriteLine("Server: Invalid XML : " + xml);
            }
        }
    }

    [Serializable]
    public class ComDataToServer : ComData
    {
        public StatusCode status { get; set; }
        public bool acceptingWork { get; set; }
        public string password { get; set; }
        public bool isWorkMessage { get; set; }
        public WorkStatus workStatus { get; set; }
        

        // Override abstract
        public override void FromXML(string xml)
        {
            string clearXml = decrypt(xml, key, salt);
            XmlSerializer serializer = new XmlSerializer(typeof(ComData));
            ComData comData = new ComDataToClient();
            StringReader sReader = new StringReader(clearXml);
            try
            {
                ComDataToServer tmpComData = (ComDataToServer)serializer.Deserialize(sReader);
                message = tmpComData.message;
                name = tmpComData.name;
                status = tmpComData.status;
                acceptingWork = tmpComData.acceptingWork;
                password = tmpComData.password;
                isWorkMessage = tmpComData.isWorkMessage;
                workStatus = tmpComData.workStatus;
            }
            catch (InvalidOperationException)
            {
                // Not XML
                Console.WriteLine("Server: Invalid XML : " + xml);
            }
        }
    }
}
