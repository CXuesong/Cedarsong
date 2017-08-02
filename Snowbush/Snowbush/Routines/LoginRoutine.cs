using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Snowbush;
using Snowbush.CommandLine;

namespace Snowbush.Routines
{
    public class LoginRoutine : IRoutine
    {
        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;

        public LoginRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<EnumPagesRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var sitePrefix = (string) arguments[0];
            if (sitePrefix == null) throw new ArgumentNullException("sitePrefix");
            var site = await siteProvider.GetSiteAsync((string) arguments[0], true);
        }
    }
}
