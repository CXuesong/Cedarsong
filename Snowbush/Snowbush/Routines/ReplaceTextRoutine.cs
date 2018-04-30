using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using Snowbush.CommandLine;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using ILogger = Serilog.ILogger;
using WikiLink = MwParserFromScratch.Nodes.WikiLink;

namespace Snowbush.Routines
{
    public class ReplaceTextRoutine : IRoutine
    {
        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;

        public ReplaceTextRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<ReplaceTextRoutine>();
        }

        /// <param name="arguments"></param>
        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var zhSite = await siteProvider.GetSiteAsync("zh", true);
            var src = (string)arguments[0];
            var dest = (string)arguments[1];
            var gen = new SearchGenerator(zhSite, src)
            {
                PaginationSize = 10
            };
            var sourceBlock = gen.EnumPagesAsync(PageQueryOptions.FetchContent).ToObservable().ToSourceBlock();
            var processorBlock = new ActionBlock<WikiPage>(p => ReplaceAsync(p, src, dest),
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            using (sourceBlock.LinkTo(processorBlock, new DataflowLinkOptions { PropagateCompletion = true }))
            {
                await processorBlock.Completion;
            }
        }

        private async Task ReplaceAsync(WikiPage page, string src, string dest)
        {
            logger.Information("Start processing: {page} .", page.Title);
            var parser = new WikitextParser();
            var root = parser.Parse(page.Content);

            void Visit(Node node)
            {
                if (node is PlainText pt)
                {
                    pt.Content = pt.Content.Replace(src, dest);
                    return;
                } else if (node is WikiLink)
                {
                    return;
                }
                foreach (var child in node.EnumChildren())
                {
                    Visit(child);
                }
            }

            Visit(root);

            var newContent = root.ToString();
            if (newContent == page.Content) return;
            Utility.ShowDiff(page.Content, newContent);
            page.Content = newContent;
            await page.UpdateContentAsync($"机器人：替换 {src}→{dest} 。", true, true);
            logger.Information("{page} saved.", page.Title);
        }
    }
}
