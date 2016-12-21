using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;

namespace Cloudtail
{
    public class ConsoleTraceListener : TraceListener
    {
        private static readonly object consoleLock = new object();

        public static readonly ConsoleTraceListener Default = new ConsoleTraceListener();

        /// <inheritdoc />
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format,
            params object[] args)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
                return;
            lock (consoleLock)
            {
                switch (eventType)
                {
                    case TraceEventType.Critical:
                    case TraceEventType.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case TraceEventType.Information:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case TraceEventType.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                }
                if (args == null)
                    Console.WriteLine("{0}:{1}", source, format);
                else if (string.IsNullOrEmpty(format))
                    Console.WriteLine("{0}:{1}", source, string.Join(",", args));
                else
                    Console.WriteLine("{0}:{1}", source, string.Format(format, args));
                Console.ResetColor();
            }
        }

        /// <inheritdoc />
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            TraceEvent(eventCache, source, eventType, id, message, null);
        }

        /// <inheritdoc />
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            base.TraceEvent(eventCache, source, eventType, id, null, null);
        }

        /// <inheritdoc />
        public override void Write(string message)
        {
            Console.Write(message);
        }

        /// <inheritdoc />
        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}
