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

        private string passWord;
        private string checkFile;

        int min;
        int max;

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

            min = minLength;
            max = maxLength;
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
            Split(batchSize);

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

        private void Split(int batchSize)
        {
            batches = new ConcurrentBag<Tuple<char[], char[]>>();

            for (int i = min; i <= max; i++)
            {
                char[] startPass = new char[i];
                char[] endPass;

                char[] charBatchSize = IntToValidChar(batchSize, i);

                for (int n = 0; n < startPass.Length; n++)
                {
                    startPass[n] = validChars.First();
                }

                if (charBatchSize.Length > startPass.Length)
                {
                    endPass = new char[i];
                    for (int n = 0; n < endPass.Length; n++)
                    {
                        endPass[n] = validChars.Last();
                    }
                    Tuple<char[], char[]> batch = new Tuple<char[], char[]>((char[])startPass.Clone(), (char[])endPass.Clone());
                    Console.WriteLine("Added batch: " + new string(batch.Item1) + " to " + new string(batch.Item2));
                    batches.Add(batch);
                    continue;
                }

                bool done = false;

                while (!done)
                {
                    endPass = AddValidChars(startPass, charBatchSize);
                    Tuple<char[], char[]> batch = new Tuple<char[], char[]>((char[])startPass.Clone(), (char[])endPass.Clone());
                    Console.WriteLine("Added batch: " + new string(batch.Item1) + " to " + new string(batch.Item2));
                    batches.Add(batch);
                    startPass = (char[])endPass.Clone();

                    if (endPass.All(c => c == validChars.Last()))
                    {
                        done = true;
                    }
                }

            }

            Console.WriteLine("Total batches: " + batches.Count);
        }

        private char[] AddValidChars(char[] vchar1, char[] vchar2)
        {
            // If resulting array is long, just return max result. i.e. {'x', 'x'} + {'x', 'x'} = {'å', 'å'}  (for lower case only)

            if (vchar1.Length != vchar2.Length)
            {
                throw new ArgumentException("Arguments must be of same length");
            }

            char[] outChar = new char[vchar1.Length];

            UInt64 vchar1Sum = 0;
            UInt64 vchar2Sum = 0;
            UInt64 maxSum = 0;

            for (int i = 0; i < vchar1.Length; i++)
            {
                vchar1Sum += (UInt64)validChars.IndexOf(vchar1[i]) * (UInt64)Math.Pow(validChars.Count, vchar1.Length - i - 1);
                vchar2Sum += (UInt64)validChars.IndexOf(vchar2[i]) * (UInt64)Math.Pow(validChars.Count, vchar1.Length - i - 1);
                maxSum += ((UInt64)validChars.Count-1) * (UInt64)Math.Pow(validChars.Count, vchar1.Length - i - 1);
            }

            if (vchar1Sum + vchar2Sum > maxSum)
            {
                for (int i = 0; i < vchar1.Length; i++)
                {
                    outChar[i] = validChars.Last();
                }
                return outChar;
            }

            int remainder = 0;
            for (int i = vchar1.Length - 1; i >= 0; i--)
            {
                int newInd = validChars.IndexOf(vchar1[i]) + validChars.IndexOf(vchar2[i]) + remainder;
                if (newInd >= validChars.Count)
                {
                    remainder = newInd / validChars.Count;
                    newInd = newInd - remainder * validChars.Count;
                }
                else
                {
                    remainder = 0;
                }
                outChar[i] = validChars[newInd];
            }
            return outChar;
        }

        private char[] IntToValidChar(int anInt, int padAmount = -1)
        {
            int len = 1;
            while (Math.Pow(validChars.Count, len) <= anInt)
            {
                len++;
            }

            char[] outChar;
            if (padAmount > len)
            {
                outChar = new char[padAmount];
            }
            else
            {
                outChar = new char[len];
                padAmount = len;
            }

            for (int i = 0; i < outChar.Length; i++)
            {
                outChar[i] = validChars.First();
            }

            for (int i = padAmount - 1; i >= 0; i--)
            {
                int increasePerIncrement = (int)Math.Pow(validChars.Count, i);
                int increments = anInt / increasePerIncrement;
                outChar[padAmount - (i + 1)] = validChars[increments];
                anInt -= increments * increasePerIncrement;
            }

            return outChar;
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
