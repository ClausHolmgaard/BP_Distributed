using System;
using System.Collections.Generic;
using System.Text;

namespace BPShared
{
    public enum ErrorCode
    {
        messageOK = 0,
        messageNOK = -1,
    }

    public enum StatusCode
    {
        idle = 0,
        processing = 1,
        errorState = -1,
    }

    public class ComData
    {
        public StatusCode status { get; set; }
        public ErrorCode error { get; set; }
        public int id { get; set; }
        public string name { get; set; }


    }
}
