using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BPShared;

namespace BPWorker
{
    public class Work
    {
        public delegate void WorkResultDelegate(List<string> results);
        public WorkResultDelegate WorkResult;

        private Thread worker;
        private string file;

        private bool useLower;
        private bool useUpper;
        private bool useNumbers;
        private bool useSymbols;
        private int size;
        private int workers;
        private Tuple<char[], char[]> aBatch;


        public Work(string filename, bool lower, bool upper, bool numbers, bool symbols, int batchSize, int workerThreads, Tuple<char[], char[]> batch)
        {
            file = filename;
            useLower = lower;
            useUpper = upper;
            useNumbers = numbers;
            useSymbols = symbols;
            size = batchSize;
            workers = workerThreads;
            aBatch = batch;
        }

        public void StartWork()
        {
            worker = new Thread(DoWork);
            worker.Start();
        }

        private void DoWork()
        {
            BreakPass bp = new BreakPass(true, false, false, false, file, false);

            List<string> res =  bp.CrackManagedExe(workers, size, aBatch);
            WorkResult(res);
        }
    }
}
