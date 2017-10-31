using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Snowbush.CommandLine;
using WikiClientLibrary;
using WikiClientLibrary.Pages;

namespace Snowbush.Routines
{
    public class BatchUploadRoutine : IRoutine
    {

        private readonly SiteProvider siteProvider;
        private readonly ILogger logger;

        public BatchUploadRoutine(ILogger logger, SiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            this.logger = logger.ForContext<BatchUploadRoutine>();
        }

        /// <inheritdoc />
        public async Task PerformAsync(CommandArguments arguments)
        {
            var rootPath = Path.GetFullPath((string)arguments[0]);
            var site = await siteProvider.GetSiteAsync("zh", true);
            var messageTempate = File.ReadAllText(Path.Combine(rootPath, "Message.txt"));
            var skippedItems = (int?)arguments["-Skip"]??0;
            foreach (var line in File.ReadLines(Path.Combine(rootPath, "Catalog.txt")))
            {
                if (skippedItems > 0)
                {
                    skippedItems--;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = line.Split('\t');
                var fileName = fields[0].Trim();
                var targetTitle = fields[1].Trim();
                var page = new FilePage(site, targetTitle);
                await page.RefreshAsync();
                if (page.Exists)
                {
                    logger.Warning("Skipped {File} because it exists.", fileName);
                    continue;
                }
                logger.Information("Uploading {File} to [[{Page}]].", fileName, page);
                var comment = messageTempate.Replace("$FILE_NAME$", fileName);
                using (var fs = File.OpenRead(Path.Combine(rootPath, fileName)))
                {
                    var source = new StreamUploadSource(fs);
                    try
                    {
                        var result = await page.UploadAsync(source, comment, false);
                        if (result.ResultCode == UploadResultCode.Warning)
                        {
                            logger.Warning("Skipped {File} due to warnings: {Warnings}.", fileName, result.Warnings);
                        }
                    }
                    catch (OperationFailedException ex)
                    {
                        if (ex.ErrorCode == "fileexists-forbidden")
                        {
                            logger.Warning("Skipped {File} because it's already uploaded.", fileName);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
        }
    }
}
