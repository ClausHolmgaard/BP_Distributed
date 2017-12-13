using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace BPShared
{ 

    public class BreakPass
    {
        private delegate bool CheckPassMethod(char[] pass);
        private CheckPassMethod CheckPass;

        private static char[] lowerCaseChars = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'æ', 'ø', 'å' };
        private static char[] upperCaseChars = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Æ', 'Ø', 'Å' };
        private static char[] numberChars = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' };
        private static char[] symbolChars = { '-', '_', ' ', ',', '.', '!' };
        private List<char> validChars;
        private char[] start;
        private char[] end;
        private string passWord;
        private string checkFile;
        //private List<Tuple<char[], char[]>> batches;
        private ConcurrentBag<Tuple<char[], char[]>> batches;
        AppDomain thisDomain = AppDomain.CurrentDomain;
        Assembly checkFileAssembly;

        public BreakPass(int minLength, int maxLength, bool lower, bool upper, bool numbers, bool symbols, string file)
        {
            checkFile = file;

            validChars = new List<char>();
            if (lower)
                validChars.AddRange(lowerCaseChars);
            if (upper)
                validChars.AddRange(upperCaseChars);
            if (numbers)
                validChars.AddRange(numberChars);
            if (symbols)
                validChars.AddRange(symbolChars);

            start = new char[minLength];
            for (int i = 0; i < minLength; i++)
            {
                start[i] = validChars.First();
            }
            end = new char[maxLength];
            for (int i = 0; i < maxLength; i++)
            {
                end[i] = validChars.Last();
            }
        }

        public void CrackAnyExe(int workers, int batchSize)
        {
            CheckPass = CheckAgainstAnyExe;
            Run(workers, batchSize);
        }

        public void CrackManagedExe(int workers, int batchSize)
        {
            CheckPass = CheckAgainstManagedExe;
            LoadAssembly();
            Run(workers, batchSize);
        }

        public void CrackZip(int workers, int batchSize)
        {
            CheckPass = CheckAgainstZip;
            Run(workers, batchSize);
        }

        public void Run(int workers, int batchSize)
        {
            Split(batchSize, start, end);

            passWord = "";

            Console.WriteLine("Starting " + workers + " workers");
            Thread[] threads = new Thread[workers];
            for (int i = 0; i < workers; i++)
            {
                threads[i] = new Thread(new ParameterizedThreadStart(processBatch));
                threads[i].IsBackground = true;
                
                threads[i].Start(i);
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }

            if (passWord.Length != 0)
            {
                Console.WriteLine("Password found: " + passWord);
            }
            else
            {
                Console.WriteLine("Password not found... :(");
            }
        }

        private void Split(int size, char[] start, char[] end)
        {
            batches = new ConcurrentBag<Tuple<char[], char[]>>();
            Tuple<char[], char[]> batch;
            bool done = false;

            while (!done)
            {
                int counter = 0;
                for (int i = start.Length; i < end.Length + 1; i++)
                {
                    char[] endPass;
                    if (i == start.Length)
                    {
                        endPass = (char[])start.Clone();
                    }
                    else
                    {
                        endPass = new char[i];
                        for (int c = 0; c < endPass.Length; c++)
                        {
                            endPass[c] = validChars.First();
                        }
                    }

                    while (counter < size)
                    {
                        counter++;

                        if (!endPass.SequenceEqual(end) && !endPass.All(c => c == validChars.Last()))
                        {
                            endPass = GetNext(endPass);
                        }
                        if (endPass.SequenceEqual(end))
                        {
                            done = true;
                        }

                    }
                    batch = new Tuple<char[], char[]>((char[])start.Clone(), (char[])endPass.Clone());
                    batches.Add(batch);
                    Console.WriteLine("Added batch: " + new string(batch.Item1) + " to " + new string(batch.Item2));
                    start = (char[])endPass.Clone();
                }
            }
            Console.WriteLine(batches.Count + " bathces in total");
        }

        private char[] GetNext(char[] pass)
        {
            for (int i = pass.Length - 1; i >= 0; i--)
            {
                if (pass[i] != validChars.Last())
                {
                    pass[i] = validChars[validChars.IndexOf(pass[i]) + 1];
                    return pass;
                }
                else
                {
                    for (int n = i; n >= 0; n--)
                    {
                        if (pass[n] != validChars.Last())
                        {
                            pass[n] = validChars[validChars.IndexOf(pass[n]) + 1];  //(char)(pass[n] + 1);
                            for (int nn = n + 1; nn < pass.Length; nn++)
                            {
                                pass[nn] = validChars.First();
                            }
                            return pass;
                        }
                    }
                }
            }

            return pass;
        }

        private char[] GetNextBatch(char[] start, int amount)
        {
            char[] end = new char[start.Length];

            // if we can't meet the amount
            if (Math.Pow(validChars.Count, start.Length) < amount)
            {
                for (int i = 0; i < start.Length; i++)
                {
                    end[i] = validChars.Last();
                }
            }
            else
            {
                for (int i = 0; i < start.Length; i++)
                {
                    int increaseAmount = (int)(Math.Pow(validChars.Count, start.Length - i - 1));
                    int increaseThisIndex = amount / increaseAmount;
                    int newInd = validChars.IndexOf(start[i]) + increaseThisIndex;
                    Console.WriteLine((int)(Math.Pow(validChars.Count, start.Length - i - 1)));
                    Console.WriteLine("newInd: " + newInd);
                    end[i] = validChars[newInd];

                    amount -= increaseAmount * increaseThisIndex;
                }
            }

            return end;

        }

        private void processBatch(object infoObj)
        {
            bool isDone = false;
            while (!isDone)
            {
                Tuple<char[], char[]> processBatch = getBatch();
                if (processBatch == null)
                {
                    isDone = true;
                    Console.WriteLine("Worker " + infoObj + " done");
                    break;
                }
                Console.WriteLine("Worker " + infoObj + ": Processing: " + new string(processBatch.Item1) + " to " + new string(processBatch.Item2));
                TrySome(processBatch.Item1, processBatch.Item2, infoObj.ToString());
            }
        }

        private Tuple<char[], char[]> getBatch()
        {

            Tuple<char[], char[]> tmpBatch;
            if(batches.TryTake(out tmpBatch))
            {
                return tmpBatch;
            }
            else
            {
                return null;
            }


        }

        private void TrySome(char[] start, char[] end, string worker)
        {
            UInt64 passChecked = 0;

            for (int i = start.Length; i < end.Length + 1; i++)
            {
                // Start array per length
                char[] testPass;
                if (i == start.Length)
                {
                    testPass = start;
                }
                else
                {
                    testPass = new char[i];
                    for (int c = 0; c < testPass.Length; c++)
                    {
                        testPass[c] = validChars.First();
                    }
                }

                while (passWord.Length == 0)
                {
                    //Console.WriteLine(testPass);
                    passChecked++;
                    bool success = CheckPass(testPass);
                    if (success)
                    {
                        passWord = new string(testPass);
                    }
                    if (!testPass.SequenceEqual(end) && !testPass.All(c => c == validChars.Last()))
                    {
                        testPass = GetNext(testPass);
                    }
                    else
                    {
                        break;
                    }

                }
            }
        }

        // Check Managed Exe
        // Very fast. Loads assembly to memory first.
        private bool CheckAgainstManagedExe(char[] pass)
        {
            int exitCode = -1;
            string[] key = { new string(pass) };
            // search for the Entry Point
            if (checkFileAssembly == null)
                return false;
            MethodInfo method = checkFileAssembly.EntryPoint;
            if (method != null)
            {
                // create an istance of the Startup form Main method
                object o = checkFileAssembly.CreateInstance(method.Name);
                // invoke the application starting point
                exitCode = (int)method.Invoke(o, new object[] { key });
            }

            if (exitCode == 1)
            {
                return true;
            }

            return false;
        }

        private void LoadAssembly()
        {
            if (checkFileAssembly == null)
            {
                // read the bytes from the application exe file
                FileStream fs;
                fs = new FileStream(checkFile, FileMode.Open);

                BinaryReader br = new BinaryReader(fs);
                byte[] bin = br.ReadBytes(Convert.ToInt32(fs.Length));
                fs.Close();
                br.Close();

                // load the bytes into Assembly
                checkFileAssembly = Assembly.Load(bin);
            }
        }

        // Check against any commandline file.
        // Very slow
        private bool CheckAgainstAnyExe(char[] pass)
        {
            Process process = new Process();
            process.StartInfo.FileName = checkFile;
            process.StartInfo.Arguments = "/C " + new string(pass);
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
            int success = process.ExitCode;
            if (success == 1)
            {
                return true;
            }

            return false;
        }

        private bool CheckAgainstZip(char[] pass)
        {

            return false;
        }
    }
}
