using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using HtmlAgilityPack;
using Microsoft.VisualBasic.CompilerServices;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using Serilog.Core;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using ILogger = Serilog.ILogger;

namespace Snowbush.Routines
{
    // Upgrade Infobox Volume usages
    // From
    //  {{Flagicon|cn}}Value-CN <br /> {{Flagicon|tw}}Value-TW
    // To
    //  {{Locale|cn|Value-CN}}{{Locale|tw|Value-TW}}
    // Remove orig_title argument.
    // Update name argument so that it contains locale-aware values.
    public class UpgradeInfoboxRoutine : IRoutine
    {
        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;

        public UpgradeInfoboxRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<UpgradeInfoboxRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync()
        {
            var zhSite = await siteProvider.GetSiteAsync("zh", true);
            var gen = new CategoryMembersGenerator(zhSite, "猫武士分卷")
            {
                MemberTypes = CategoryMemberTypes.Page,
                PagingSize = 10
            };
            var sourceBlock = gen.EnumPagesAsync(PageQueryOptions.FetchContent).ToObservable().ToSourceBlock();
            var processorBlock = new ActionBlock<Page>(UpgradeVolumeIB,
                new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = 2});
            using (sourceBlock.LinkTo(processorBlock, new DataflowLinkOptions { PropagateCompletion = true }))
            {
                await processorBlock.Completion;
            }
        }

        private async Task UpgradeVolumeIB(Page page)
        {
            logger.Information("Start processing {page}", page.Title);
            var parser = new WikitextParser();
            var root = parser.Parse(page.Content);
            var ib = root.EnumDescendants()
                .OfType<Template>()
                .First(t => MwParserUtility.NormalizeTitle(t.Name) == "Infobox volume");
            ib.Arguments["orig_title"]?.Remove();
            var nameArgument = ib.Arguments["name"];
            //if (nameArgument.EnumDescendants()
            //    .OfType<Template>()
            //    .Any(t => MwParserUtility.NormalizeTitle(t) == "Locale"))
            //{
            //    logger.Information("Skipped {page}", page.Title);
            //    return;
            //}
            var nameLine = new Paragraph();
            nameLine.Append(" ");
            nameArgument.Value.Lines.Clear();
            nameArgument.Value.Lines.Add(nameLine);
            var parsedPage = await page.Site.ParsePageAsync(page.Title, "zh-tw", ParsingOptions.EffectiveLanguageLinks,
                CancellationToken.None);
            var en = parsedPage.Interlanguages.FirstOrDefault(l => l.Language == "en")?.PageTitle;
            if (en != null)
            {
                nameLine.Inlines.Add(new Template(new Run(new PlainText("Locale")))
                {
                    Arguments =
                    {
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText("us")))),
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText(en)))),
                    }
                });
            }
            if (!Regex.IsMatch(page.Title, @"[A-Za-z]"))
            {
                nameLine.Inlines.Add(new Template(new Run(new PlainText("Locale")))
                {
                    Arguments =
                    {
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText("cn")))),
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText(page.Title)))),
                    }
                });
                nameLine.Inlines.Add(new Template(new Run(new PlainText("Locale")))
                {
                    Arguments =
                    {
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText("tw")))),
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText(parsedPage.DisplayTitle)))),
                    }
                });
            }
            nameLine.Append("  \n");

            //var engname = root.EnumDescendants()
            //    .OfType<Template>()
            //    .First(t => MwParserUtility.NormalizeTitle(t.Name) == "Engname" &&
            //                t.Arguments[2].ToString().StartsWith("''"));
            //engname.Arguments[1].Value = new Wikitext(new Paragraph(new PlainText("《{{LCPAGENAME|zh}}》")));

            var newText = root.ToString();
            newText = Regex.Replace(newText, @"(\s*)\{\{flagicon\|(\w+)\}\}(.*?)(<br />|\n)", m =>
            {
                var s = m.Groups[3].Value;
                if (string.IsNullOrWhiteSpace(s))
                {
                    s = "";
                }
                else
                {
                    s = "{{Locale|" + m.Groups[2].Value.ToLowerInvariant() + "|" + s.Trim() + "}}";
                }
                if (m.Groups[4].Value == "\n") s += "\n";
                return m.Groups[1].Value + s;
            }, RegexOptions.IgnoreCase);
            Utility.ShowDiff(page.Content, newText);
            page.Content = newText;
            await page.UpdateContentAsync("机器人：使用{{Locale}}模板。", true, true);
            logger.Information("Saved: {title}", page.Title);
        }
    }
}
