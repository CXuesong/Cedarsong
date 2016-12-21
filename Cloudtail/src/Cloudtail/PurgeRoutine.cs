using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WikiClientLibrary;

namespace Cloudtail
{
    public class PurgeRoutine
    {

        public PurgeRoutine(SiteProvider siteProvider)
        {
            SiteProvider = siteProvider;
        }

        public SiteProvider SiteProvider { get; }

        public async Task PurgeZhAsync(IEnumerable<string> expression, bool updateLink, bool purgeCategory)
        {
            var purgeOptions = updateLink ? PagePurgeOptions.ForceLinkUpdate : PagePurgeOptions.None;
            var site = SiteProvider.ZhSite;
            var pages = expression.Select(exp => Page.FromTitle(site, exp)).ToArray();
            if (updateLink)
                Logger.Cloudtail.Info(this, "ForceLinkUpdate: ON", pages.Length);
            Logger.Cloudtail.Info(this, "Purging {0} pages…", pages.Length);
            var failedPages = await pages.PurgeAsync(purgeOptions);
            Logger.Cloudtail.Info(this, "Purged {0} pages.", pages.Length - failedPages.Count);
            if (failedPages.Count > 0)
                Logger.Cloudtail.Warn(this, "Failed to purge: {0}", string.Join(",", failedPages));
            if (purgeCategory)
            {
                foreach (var cat in pages.OfType<Category>())
                {
                    await cat.RefreshAsync();
                    Logger.Cloudtail.Info(this, "Purging {0}…", cat);
                    await cat.EnumMembersAsync(PageQueryOptions.None)
                        .Buffer(10)
                        .ForEachAsync(async p =>
                        {
                            var fp = await p.PurgeAsync(purgeOptions);
                            Logger.Cloudtail.Info(this, "Purged {0} pages.", p.Count - fp.Count);
                            if (fp.Count > 0)
                                Logger.Cloudtail.Warn(this, "Failed to purge: {0}", string.Join(",", fp));
                        });
                }
            }
        }
    }
}
