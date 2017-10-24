using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using Serilog;
using Snowbush.CommandLine;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiLink = MwParserFromScratch.Nodes.WikiLink;

namespace Snowbush.Routines
{
    public class RenamePageRoutine : IRoutine
    {
        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;
        private Dictionary<string, string> titles;
        private readonly WikitextParser parser = new WikitextParser();

        public RenamePageRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<EnumPagesRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            titles = arguments.PositionalArguments.Select(arg =>
            {
                var fields = arg.Value.Split('|');
                if (fields.Length != 2)
                    throw new ArgumentException("Invalid title pair. Should be oldTitle|newTitle.", nameof(arguments));
                return fields;
            }).ToDictionary(f => MwParserUtility.NormalizeTitle(f[0]), f => MwParserUtility.NormalizeTitle(f[1]));
            if (titles.Count == 0) return;
            var site = await siteProvider.GetSiteAsync("zh");
            var pages = titles.ToAsyncEnumerable().SelectMany(p =>
                    new BacklinksGenerator(site, p.Key).EnumItemsAsync()
                        .Concat(new TranscludedInGenerator(site, p.Value).EnumItemsAsync()))
                .Distinct()
                .Select(stub => new WikiPage(site, stub.Title));
            if ((bool)arguments["Move"])
            {
                logger.Information("Will move the pages.");
                foreach (var p in titles)
                {
                    try
                    {
                        var page = new WikiPage(site, p.Key);
                        await page.MoveAsync(p.Value, "重命名：" + p + "。");
                    }
                    catch (WikiClientException ex)
                    {
                        logger.Warning("Can't move {Pair}: {Error}", p, ex.Message);
                    }
                }
            }
            using (var ienu = pages.Buffer(50).GetEnumerator())
            {
                while (await ienu.MoveNext())
                {
                    var batch = ienu.Current;
                    logger.Information("Fixing links on: {Pages}", batch);
                    await batch.RefreshAsync(PageQueryOptions.FetchContent);
                    await Task.WhenAll(batch.Select(ReplaceLinksAsync));
                }
            }
        }

        public async Task ReplaceLinksAsync(WikiPage page)
        {
            var root = parser.Parse(page.Content);
            var replacedTitles = new HashSet<string>();
            foreach (var link in root.EnumDescendants().OfType<WikiLink>())
            {
                var oldTarget = link.Target.ToString();
                var oldTitle = MwParserUtility.NormalizeTitle(oldTarget);
                if (string.IsNullOrEmpty(oldTitle)) continue;
                if (titles.TryGetValue(oldTitle, out var newTitle))
                {
                    link.Target = new Run(new PlainText(ReplaceKeepWhitespaces(oldTarget, newTitle)));
                    replacedTitles.Add(oldTitle);
                }
            }
            foreach (var template in root.EnumDescendants().OfType<Template>())
            {
                var oldTarget = template.Name.ToString();
                var oldTransclusion = MwParserUtility.NormalizeTitle(oldTarget);
                if (string.IsNullOrEmpty(oldTransclusion)) continue;
                var oldTitle = WikiClientLibrary.WikiLink.NormalizeWikiLink(page.Site, oldTransclusion, BuiltInNamespaces.Template);
                if (titles.TryGetValue(oldTitle, out var newTitle))
                {
                    var newTransclusion = newTitle;
                    var newLink = WikiClientLibrary.WikiLink.Parse(page.Site, newTitle, BuiltInNamespaces.Template);
                    if (newLink.Namespace.Id == BuiltInNamespaces.Template)
                        newTransclusion = newLink.Title;
                    else if (newLink.Namespace.Id == BuiltInNamespaces.Main)
                        newTransclusion = ":" + newTransclusion;
                    template.Name = new Run(new PlainText(ReplaceKeepWhitespaces(oldTarget, newTransclusion)));
                    replacedTitles.Add(oldTitle);
                }
            }
            var newText = root.ToString();
            if (page.Content == newText) return;
            Utility.ShowDiff(page.Content, newText);
            page.Content = newText;
            var sb = new StringBuilder("页面重命名：");
            var isFirst = true;
            foreach (var t in replacedTitles)
            {
                if (isFirst)
                    isFirst = false;
                else
                    sb.Append('、');
                sb.Append(t);
                sb.Append('→');
                sb.Append(titles[t]);
            }
            sb.Append('。');
            await page.UpdateContentAsync(sb.ToString(), true, true);
        }

        /// <summary>
        /// Replace the text content, keeping leading and trailing white-space intact.
        /// </summary>
        private static string ReplaceKeepWhitespaces(string originalText, string newText)
        {
            newText = newText?.Trim();
            if (string.IsNullOrEmpty(originalText)) return newText;
            int left, right;
            for (left = 0; left < originalText.Length; left++)
                if (!char.IsWhiteSpace(originalText, left)) goto HAS_TEXT;
            return newText;
            HAS_TEXT:
            for (right = originalText.Length; right > 0; right--)
                if (!char.IsWhiteSpace(originalText, right - 1)) break;
            return originalText.Substring(0, left) + newText + originalText.Substring(right, originalText.Length - right);
        }

    }
}
