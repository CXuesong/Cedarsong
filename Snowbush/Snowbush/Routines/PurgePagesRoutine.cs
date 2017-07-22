using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Snowbush.CommandLine;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using ILogger = Serilog.ILogger;

namespace Snowbush.Routines
{
    public class PurgePagesRoutine : IRoutine
    {

        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;

        public PurgePagesRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<PurgePagesRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var freePages = ((string) arguments["T"])?.Split('|')
                .Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            var categories = ((string) arguments["C"])?.Split('|')
                .Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            var purgeLinks = (bool?) arguments["F"] ?? false;
            if (freePages == null && categories == null) return;
            var site = await siteProvider.GetSiteAsync("zh");
            if (freePages != null)
            {
                logger.Information("Purging {count} free pages…", freePages.Length);
                var failed = await freePages.Select(t => WikiPage.FromTitle(site, t)).PurgeAsync(purgeLinks
                    ? PagePurgeOptions.ForceLinkUpdate
                    : PagePurgeOptions.None);
                if (failed.Count > 0)
                {
                    logger.Warning("Failed to purge: {pages}", failed);
                }
            }
            if (categories != null)
            {
                foreach (var c in categories)
                {
                    var generator = new CategoryMembersGenerator(site, c);
                    logger.Information("Purging category: {title}…", WikiLink.NormalizeWikiLink(site, c));
                    // TODO async
                    await generator.EnumPagesAsync().Buffer(20).ForEachAsync(pages =>
                        pages.PurgeAsync(purgeLinks
                            ? PagePurgeOptions.ForceLinkUpdate
                            : PagePurgeOptions.None).Wait()
                    );
                }
            }
        }
    }
}
