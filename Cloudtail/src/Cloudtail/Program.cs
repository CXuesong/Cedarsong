using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiDiffSummary;

namespace Cloudtail
{
    public class Program
    {

        public static void Main(string[] args)
        {
            var duty = new Routine();
            duty.PerformAsync().Wait();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Finished");
            Console.ResetColor();
        }

    }
}
