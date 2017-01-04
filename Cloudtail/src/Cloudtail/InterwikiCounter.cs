using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MwParserFromScratch;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using LinkNode = MwParserFromScratch.Nodes.WikiLink;

namespace Cloudtail
{
    public class InterwikiCounter
    {
        public InterwikiCounter(SiteProvider siteProvider)
        {
            if (siteProvider == null) throw new ArgumentNullException(nameof(siteProvider));
            SiteProvider = siteProvider;
        }

        public SiteProvider SiteProvider { get; }

        public async Task PerformAsync()
        {
            var site = SiteProvider.EnSite;
            var cat = new CategoryMembersGenerator(site, "Characters") { PagingSize = 50 };
            var parsers = new ObjectPool<WikitextParser>(() => new WikitextParser());
            await cat.EnumPagesAsync(PageQueryOptions.FetchContent).ForEachAsync(page =>
            {
                var parser = parsers.Get();
                var root = parser.Parse(page.Content);
                var count = root.EnumDescendants().OfType<LinkNode>()
                    .Select(link => link.Target.ToString())
                    .Where(target => !target.StartsWith(":") && !target.StartsWith("#"))
                    .Select(target =>
                    {
                        try
                        {
                            return WikiLink.Parse(site, target);
                        }
                        catch (ArgumentException)
                        {
                            Logger.Cloudtail.Warn(page, "Cannot parse link: {0}", target);
                            return null;
                        }
                    })
                    .Count(l => l?.Interwiki != null);
                if (count < 3)
                    Logger.Cloudtail.Info(page, "Interwikis: {0}", count);
            });
        }
    }
}
