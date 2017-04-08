using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Snowbush.Routines
{
    public class UpgradeInfoboxRoutine : IRoutine
    {
        private ILogger logger;

        public UpgradeInfoboxRoutine(ILogger logger)
        {
            this.logger = logger.ForContext<UpgradeInfoboxRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync()
        {
            logger.Information("Start");
        }
    }
}
