using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BPShared;

namespace BPCoordinator
{
    public class HandleWorkers
    {
        public delegate void PasswordFoundDelegate(string password);
        public delegate void ProcessingDoneDelegate(List<string> passwords);
        public delegate void BatchUpdateDelegate();
        public delegate List<NetworkClient> GetClientsDelegate();
        public delegate void SendClientWorkDelegate(int clientId, string file, char[] start, char[] end, bool lower, bool upper, bool numbers, bool symbols);

        public PasswordFoundDelegate PasswordFound;
        public ProcessingDoneDelegate ProcessingDone;
        public BatchUpdateDelegate BatchUpdate;
        public GetClientsDelegate GetClients;
        public SendClientWorkDelegate SendClientWork;

        private List<char> validChars;
        private bool lowerChars;
        private bool upperChars;
        private bool numberChars;
        private bool symbolChars;

        // Lists are not thread safe, these are. Non ordered.
        private ConcurrentBag<Tuple<char[], char[]>> batchesNotProcessed;
        private ConcurrentBag<Tuple<char[], char[]>> batchesProcessed;
        private ConcurrentBag<Tuple<char[], char[]>> bathcesProcessing;

        // Ready to handle work
        public bool isReady { get; private set; }
        // working
        public bool doWork { get; private set; }

        List<string> passwordsFound;

        Thread workThread;

        // Params for starting split thread
        private class ThreadParams
        {
            public int min;
            public int max;
            public int batchSize;
        }

        // constructor, will start worker handling
        public HandleWorkers(int min, int max, int batchSize, string fileName, bool lower, bool upper, bool numbers, bool symbols)
        {
            isReady = false;

            lowerChars = lower;
            upperChars = upper;
            numberChars = numbers;
            symbolChars = symbols;

            validChars = new List<char>();
            if (lower)
                validChars.AddRange(BreakPass.lowerCaseChars);
            if (upper)
                validChars.AddRange(BreakPass.upperCaseChars);
            if (numbers)
                validChars.AddRange(BreakPass.numberChars);
            if (symbols)
                validChars.AddRange(BreakPass.symbolChars);

            batchesNotProcessed = new ConcurrentBag<Tuple<char[], char[]>>();
            batchesProcessed = new ConcurrentBag<Tuple<char[], char[]>>();
            bathcesProcessing = new ConcurrentBag<Tuple<char[], char[]>>();

            ThreadParams p = new ThreadParams();
            p.min = min;
            p.max = max;
            p.batchSize = batchSize;

            Thread splitThread = new Thread(new ParameterizedThreadStart(SplitBatchThread));
            splitThread.SetApartmentState(ApartmentState.STA);
            splitThread.Start(p);

            doWork = true;
            workThread = new Thread(new ParameterizedThreadStart(DoWorkThread));
            workThread.Start(fileName);
        }

        // return batches not yet processed as a list
        public List<Tuple<char[], char[]>> GetBatchesNotProcessed()
        {
            List<Tuple<char[], char[]>> b = new List<Tuple<char[], char[]>>();
            foreach (Tuple<char[], char[]> t in batchesNotProcessed)
            {
                b.Add(t);
            }

            return b;
        }

        // return batches processed as a list
        public List<Tuple<char[], char[]>> GetBatchesProcessed()
        {
            List<Tuple<char[], char[]>> b = new List<Tuple<char[], char[]>>();
            foreach (Tuple<char[], char[]> t in batchesProcessed)
            {
                b.Add(t);
            }

            return b;
        }

        // return batches processing as a list
        public List<Tuple<char[], char[]>> GetBatchesProcessing()
        {
            List<Tuple<char[], char[]>> b = new List<Tuple<char[], char[]>>();
            foreach (Tuple<char[], char[]> t in bathcesProcessing)
            {
                b.Add(t);
            }

            return b;
        }

        // Stop handling workers
        public void StopWork()
        {
            doWork = false;
            workThread.Join();
        }

        // Get batches to process
        private void SplitBatchThread(object infoObj)
        {
            ThreadParams p = (ThreadParams)infoObj;
            int min = p.min;
            int max = p.max;
            int batchSize = p.batchSize;

            char[] start = new char[min];
            char[] end = new char[max];

            for (int i = 0; i < start.Length; i++)
            {
                start[i] = validChars.First();
            }
            for (int i = 0; i < end.Length; i++)
            {
                end[i] = validChars.Last();
            }

            List<Tuple<char[], char[]>> b = BreakPass.Split(batchSize, start, end, validChars);
            foreach (Tuple<char[], char[]> t in b)
            {
                batchesNotProcessed.Add(t);
            }

            isReady = true;

            BatchUpdate();
        }

        // Thread wjere worker handling happens
        private void DoWorkThread(object infoObj)
        {
            List<NetworkClient> clients;
            string filename = (string)infoObj;

            while (!isReady)
            {
                Thread.Sleep(100);
            }

            Console.WriteLine("Assigning tasks to workers");

            while(doWork)
            {
                clients = GetClients();

                if(clients == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                foreach(NetworkClient c in clients)
                {
                    // Send work to client, if it's accepting
                    if(c.acceptingWork && c.status == StatusCode.idle)
                    {
                        Tuple<char[], char[]> tmpBatch;
                        if (batchesNotProcessed.TryTake(out tmpBatch))
                        {
                            BatchUpdate();
                            Console.WriteLine("Sending work to client " + c.Id);
                            SendClientWork(c.Id, filename, tmpBatch.Item1, tmpBatch.Item2, lowerChars, upperChars, numberChars, symbolChars);
                        }
                        
                    }
                }
                Thread.Sleep(1000);
            }
        }
    }
}
