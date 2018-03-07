using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using Serilog;
using Snowbush.CommandLine;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;

namespace Snowbush.Routines
{
    public class ReplaceText2Routine : IRoutine
    {

        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;
        private readonly WikitextParser parser = new WikitextParser();

        public ReplaceText2Routine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<ReplaceTextRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var site = await siteProvider.GetSiteAsync("zh", true);
            var gen = new CategoryMembersGenerator(site, "猫物")
            {
                PaginationSize = 50,
                MemberTypes = CategoryMemberTypes.Page,
            };
            using (var ie = gen.EnumPagesAsync(PageQueryOptions.FetchContent).Buffer(50).GetEnumerator())
            {
                while (await ie.MoveNext())
                {
                    var batch = ie.Current;
                    foreach (var p in batch)
                    {
                        logger.Information("Process {Page}", p);
                        await ProcessPage(p);
                    }
                }
            }
        }

        private async Task ProcessPage(WikiPage page)
        {
            if (page.IsRedirect)
            {
                logger.Information("{Page} is redirect.", page);
                return;
            }
            var changed = false;
            var root = parser.Parse(page.Content);
            var node = root.EnumDescendants().OfType<Template>().FirstOrDefault(t => MwParserUtility.NormalizeTitle(t.Name) == "Quote");
            if (node == null)
            {
                logger.Information("No Quote found.", page);
                return;
            }
            var src = node.Arguments[3];
            if (src == null) return;
            foreach (var pt in src.EnumDescendants().OfType<PlainText>())
            {
                var nt = pt.Content.Replace("花絮", "番外");
                if (nt != pt.Content)
                {
                    pt.Content = nt;
                    changed = true;
                }
            }
            var newText = root.ToString();
            if (changed)
            {
                Utility.ShowDiff(page.Content, newText);
                page.Content = newText;
                await page.UpdateContentAsync("机器人：替换 花絮 -> 番外。", true, true);
            }
        }

    }
}
