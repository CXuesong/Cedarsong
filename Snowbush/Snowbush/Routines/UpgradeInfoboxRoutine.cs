using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Snowbush.Routines
{
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

        }
    }
}
