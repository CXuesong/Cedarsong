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

        public const string EnApiEntryPoint = "http://warriors.wikia.com/api.php";
        public const string ZhApiEntryPoint = "http://warriors.huiji.wiki/api.php";

        public static void Main(string[] args)
        {
            RoutineAsync().Wait();
        }

        public static async Task RoutineAsync()
        {
            var client = new WikiClient();
            var siteEn = await Site.CreateAsync(client, EnApiEntryPoint);
            //var siteZh = await Site.CreateAsync(client, ZhApiEntryPoint);
            var page = Page.FromTitle(siteEn, "Graystripe");
            var revs = await page.EnumRevisionsAsync(2, PageQueryOptions.FetchContent).Take(2).ToList();
            var cmp = new WikitextBySectionComparer();
            var diff = cmp.Compare(revs[1].Content, revs[0].Content);
            foreach (var d in diff)
            {
                if (d.Status != SectionDiffStatus.Identical)
                {
                    Console.WriteLine(d);
                }
            }
        }
    }
}
