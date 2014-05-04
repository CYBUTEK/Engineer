using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Engineer
{
    public class LogMsg
    {
        public StringBuilder buf;

        public LogMsg()
        {
            buf = new StringBuilder();
        }

        public void Flush()
        {
            MonoBehaviour.print(buf);
            buf.Length = 0;
        }
    }
}
