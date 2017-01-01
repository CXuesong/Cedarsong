using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Logger.Cloudtail.Listeners.Add(ConsoleTraceListener.Default);
            Logger.Cloudtail.Switch.Level = SourceLevels.All;
            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), WorkDir));
            Logger.Cloudtail.Info(null, "Work path = {0}", Directory.GetCurrentDirectory());
            ////
            Func<string, bool> hasSwitch =
                sw => args.Skip(1).Any(a => string.Equals(a, "/" + sw, StringComparison.OrdinalIgnoreCase));
            ////
            var siteProvider = new SiteProvider("cookies.json");
            if (args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "login":
                        siteProvider.EnsureLoggedIn(siteProvider.ZhSite);
                        break;
                    case "test":
                        var site = siteProvider.ZhSite;
                        siteProvider.SaveSession();
                        break;
                    case "sync":
                    {
                        var duty = new Routine(siteProvider);
                        duty.PerformAsync().Wait();
                    }
                        break;
                    case "purge":
                    {
                        var routine = new PurgeRoutine(siteProvider);
                        routine.PurgeZhAsync(args.Skip(1).Where(s => !s.StartsWith("/")),
                            hasSwitch("L"), hasSwitch("C")).Wait();
                    }
                        break;
                    case "publications":
                        {
                            var routine = new GatherPublications(siteProvider);
                            if (args.Length > 1 && args[1] == "export")
                                routine.ExportModulesAsync().Wait();
                            else
                                routine.FetchAsync().Wait();
                        }
                        break;
                    default:
                        Console.WriteLine("Unknown action: {0}.", args[0]);
                        return 1;
                }
            }

            ////
            Logger.Cloudtail.Info(null, "Finished");
            return 0;
        }
    }
}
