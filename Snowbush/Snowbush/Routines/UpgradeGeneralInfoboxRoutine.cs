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
using Snowbush.CommandLine;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Parsing;
using WikiClientLibrary.Sites;
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
    public class UpgradeGeneralInfoboxRoutine : IRoutine
    {
        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;
        private int counter;

        public UpgradeGeneralInfoboxRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<UpgradeGeneralInfoboxRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var zhSite = await siteProvider.GetSiteAsync("zh", true);
            //var gen = new CategoryMembersGenerator(zhSite, "猫武士分卷")
            //var gen = new CategoryMembersGenerator(zhSite, "猫武士故事")
            //var gen = new CategoryMembersGenerator(zhSite, "猫武士短文")
            var gen = new CategoryMembersGenerator(zhSite, "猫物")
            {
                MemberTypes = CategoryMemberTypes.Page,
                PaginationSize = 20
            };
            counter = 0;
            var sourceBlock = gen.EnumPagesAsync(PageQueryOptions.FetchContent).ToObservable().ToSourceBlock();
            var processorBlock = new ActionBlock<WikiPage>(UpgradeVolumeIB,
                new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = 2});
            using (sourceBlock.LinkTo(processorBlock, new DataflowLinkOptions {PropagateCompletion = true}))
            {
                await processorBlock.Completion;
            }
        }

        private async Task UpgradeVolumeIB(WikiPage page)
        {
            var ct = Interlocked.Increment(ref counter);
            if (ct < 50) return;
            logger.Information("Start processing {page} (#{counter})", page.Title, ct);
            var parser = new WikitextParser();
            var root = parser.Parse(page.Content);
            var ib = root.EnumDescendants()
                .OfType<Template>()
                .First(t => MwParserUtility.NormalizeTitle(t.Name).StartsWith("Infobox "));
            ib.Arguments["orig_title"]?.Remove();
            var nameOthersArgument = ib.Arguments["name_others"];
            var nameOthersExpr = "";
            if (nameOthersArgument != null)
            {
                nameOthersArgument.Remove();
                var value = nameOthersArgument.Value.ToString().Trim();
                if (value != "")
                {
                    logger.Warning("name_others detected on {page} .", page.Title);
                    nameOthersExpr = "、-{" + value + "}-";
                }
            }
            var nameArgument = ib.Arguments["name"];
            if (nameArgument.ToString().IndexOf("Ruby", StringComparison.OrdinalIgnoreCase) >= 0)
                logger.Warning("ruby detected on {page} ({content}).", page.Title, nameArgument.ToString());
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
            var en = parsedPage.LanguageLinks.FirstOrDefault(l => l.Language == "en")?.Title;
            if (en != null)
            {
                nameLine.Inlines.Add(new Template(new Run(new PlainText("Locale")))
                {
                    Arguments =
                    {
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText("us")))),
                        new TemplateArgument(null,
                            new Wikitext(new Paragraph(new PlainText(Utility.BareDisambigTitle(en))))),
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
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText("-{" + Utility.BareDisambigTitle(page.Title )+ "}-" + nameOthersExpr)))),
                    }
                });
                nameLine.Inlines.Add(new Template(new Run(new PlainText("Locale")))
                {
                    Arguments =
                    {
                        new TemplateArgument(null, new Wikitext(new Paragraph(new PlainText("tw")))),
                        new TemplateArgument(null,
                            new Wikitext(new Paragraph(
                                new PlainText("-{" + Utility.BareDisambigTitle(parsedPage.DisplayTitle) + "}-" +
                                              nameOthersExpr)))),
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
            newText = newText.Replace("={{Locale|", "= {{Locale|");
            if (page.Content != newText)
            {
                Utility.ShowDiff(page.Content, newText);
                page.Content = newText;
                await page.UpdateContentAsync("机器人：使用{{Locale}}模板。", true, true);
                logger.Information("Saved: {title}", page.Title);
            }
            else
            {
                logger.Information("Nothing changed: {title}", page.Title);
            }
        }
    }
}
