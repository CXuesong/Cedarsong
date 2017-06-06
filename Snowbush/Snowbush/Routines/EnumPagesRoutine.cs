using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Snowbush.CommandLine;
using WikiClientLibrary.Generators;

namespace Snowbush.Routines
{
    /// <summary>
    /// Enumerates all the page titles on the site.
    /// </summary>
    public class EnumPagesRoutine : IRoutine
    {
        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;

        public EnumPagesRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<EnumPagesRoutine>();
        }

        /// <param name="arguments"></param>
        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var outputPath = (string) arguments[0];
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));
            var zhSite = await siteProvider.GetSiteAsync("zh");
            using (var sw = File.CreateText(outputPath))
            {
                var generator = new AllPagesGenerator(zhSite) {PagingSize = 100};
                foreach (var ns in zhSite.Namespaces)
                {
                    if (ns.Id < 0) continue;
                    logger.Information("Enum in {namespace}…", ns);
                    generator.NamespaceId = ns.Id;
                    await generator.EnumPagesAsync().ForEachAsync(p => sw.WriteLine(p.Title));
                }
            }
        }
    }
}
