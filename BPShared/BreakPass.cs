using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BPShared
{
    public class BreakPass
    {
        private static char[] lowerCaseChars = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'æ', 'ø', 'å' };
        private static char[] upperCaseChars = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Æ', 'Ø', 'Å' };
        private static char[] numberChars = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' };
        private static char[] symbolChars = { '-', '_', ' ', ',', '.', '!' };
        private List<char> validChars;
        private string passWord;
        private string checkFile;
        private List<Tuple<char[], char[]>> batches;
        private AppDomain encryptedFile;

        public BreakPass(bool lower, bool upper, bool numbers, bool symbols)
        {
            validChars = new List<char>();
            if (lower)
                validChars.AddRange(lowerCaseChars);
            if (upper)
                validChars.AddRange(upperCaseChars);
            if (numbers)
                validChars.AddRange(numberChars);
            if (symbols)
                validChars.AddRange(symbolChars);
        }

        private void Split(int size, char[] start, char[] end)
        {
            batches = new List<Tuple<char[], char[]>>();
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

        public void run(int workers, string passFileString)
        {
            encryptedFile =  AppDomain.CreateDomain("New Appdomain");

            char[] start = { 'a' };
            char[] end = { 'z', 'z', 'z', 'z' };
            Split(10000, start, end);

            passWord = "";
            checkFile = passFileString;

            Console.WriteLine("Starting " + workers + " workers");
            Thread[] threads = new Thread[workers];
            for (int i = 0; i < workers; i++)
            {
                threads[i] = new Thread(new ParameterizedThreadStart(processBatch));
                //threads[i] = new Thread(new ThreadStart(processBatch));
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

        private void processBatch(object infoObj)
        {
            bool isDone = false;
            while (!isDone)
            {
                Tuple<char[], char[]> processBatch = getBatch();
                if (processBatch == null)
                {
                    isDone = true;
                    break;
                }
                Console.WriteLine("Worker " + infoObj + ": Processing: " + new string(processBatch.Item1) + " to " + new string(processBatch.Item2));
                TrySome(processBatch.Item1, processBatch.Item2, infoObj.ToString());
            }
        }

        private Tuple<char[], char[]> getBatch()
        {
            try
            {
                Tuple<char[], char[]> tmpBatch = batches.First();
                batches.RemoveAt(0);
                return tmpBatch;
            }
            catch (InvalidOperationException)
            {
                // Another thread took the last batch
                return null;
            }
            
        }

        private void TrySome(char[] start, char[] end, string worker)
        {
            UInt64 printEvery = 30000;
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
                        testPass[c] = 'a';
                    }
                }

                while (passWord.Length == 0)
                {
                    //Console.WriteLine(testPass);
                    passChecked++;
                    bool success = CheckPass(testPass, encryptedFile);
                    if (passChecked % printEvery == 0)
                    {
                        Console.WriteLine("Worker " + worker + ": " + passChecked + " Passes checked, currenly at: " + new string(testPass));
                    }
                    if (success)
                    {
                        passWord = new string(testPass);
                    }
                    if (!testPass.SequenceEqual(end) && !testPass.All(c => c == 'z'))
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

        private bool CheckPass(char[] pass, AppDomain dom)
        {
            string[] key = { new string(pass) };
            int success = dom.ExecuteAssembly(checkFile, key);
            if (success == 1)
            {
                return true;
            }
            return false;
        }

    }
}
