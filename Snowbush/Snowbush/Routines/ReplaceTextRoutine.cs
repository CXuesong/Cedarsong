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
using ILogger = Serilog.ILogger;

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
            //var gen = new CategoryMembersGenerator(zhSite, "猫武士分卷")
            //var gen = new CategoryMembersGenerator(zhSite, "猫武士故事")
            var gen = new CategoryMembersGenerator(zhSite, "猫武士短文")
            {
                MemberTypes = CategoryMemberTypes.Page,
                PagingSize = 10
            };
            var sourceBlock = gen.EnumPagesAsync(PageQueryOptions.FetchContent).ToObservable().ToSourceBlock();
            var processorBlock = new ActionBlock<Page>(ReplaceAsync,
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            using (sourceBlock.LinkTo(processorBlock, new DataflowLinkOptions { PropagateCompletion = true }))
            {
                await processorBlock.Completion;
            }
        }

        private async Task ReplaceAsync(Page page)
        {
            logger.Information("Start processing: {page} .", page.Title);
            var parser = new WikitextParser();
            var root = parser.Parse(page.Content);
            foreach (var locale in root.EnumDescendants()
                .OfType<Template>()
                .Where(t => MwParserUtility.NormalizeTitle(t.Name) == "Locale"))
            {
                Node node = locale;
                while (node != null)
                {
                    if (node is TemplateArgument arg)
                    {
                        if (MwParserUtility.NormalizeTemplateArgumentName(arg.Name) == "name") goto EVAL;
                        break;
                    }
                    node = node.ParentNode;
                }
                continue;
                EVAL:
                var lang = locale.Arguments[1].Value.ToString().Trim().ToLowerInvariant();
                if (lang == "cn" || lang == "tw")
                {
                    var value = locale.Arguments[2].ToString().Trim();
                    if (value.StartsWith("-{")) continue;
                    locale.Arguments[2].Value = new Wikitext(new Paragraph(new PlainText("-{" + value + "}-")));
                }
            }
            var newContent = root.ToString();
            if (newContent == page.Content) return;
            Utility.ShowDiff(page.Content, newContent);
            page.Content = newContent;
            await page.UpdateContentAsync("机器人：禁用name属性的字形转换。", true, true);
            logger.Information("{page} saved.", page.Title);
        }
    }
}
