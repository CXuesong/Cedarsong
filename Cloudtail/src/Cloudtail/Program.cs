using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiDiffSummary;

namespace Cloudtail
{
    public class Program
    {

        public static string WorkDir = "work";

        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), WorkDir));
            Console.WriteLine("Work path = {0}", Directory.GetCurrentDirectory());
            ////
            var siteProvider = new SiteProvider("cookies.json");
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "login":
                        siteProvider.EnsureLoggedIn(siteProvider.ZhSite);
                        return 0;
                    case "test":
                        var site = siteProvider.ZhSite;
                        siteProvider.SaveSession();
                        return 0;
                    default:
                        Console.WriteLine("Unknown action: {0}.", args[0]);
                        return 1;
                }
            }
            var duty = new Routine(siteProvider);
            duty.PerformAsync().Wait();
            ////
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Finished");
            Console.ResetColor();
            return 0;
        }

    }
}
