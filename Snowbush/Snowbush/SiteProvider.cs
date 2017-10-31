using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Infrastructures;
using WikiClientLibrary.Sites;

namespace Snowbush
{
    public class SiteProvider
    {
        public WikiClient WikiClient { get; }

        public WikiFamily WikiFamily { get; }

        private readonly Serilog.ILogger logger;
        private readonly LoggerFactory loggerFactory;
        private readonly IAccountAssertionFailureHandler accountAssertionFailureHandler;


        private static readonly JsonSerializer cookieSerializer = new JsonSerializer
        {
            ContractResolver = new AllPrivateFieldsContractResolver()
        };

        public SiteProvider(string cookiesFileName, Serilog.ILogger logger)
        {
            this.logger = logger.ForContext<SiteProvider>();
            loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog(logger);
            CookiesFileName = cookiesFileName;
            accountAssertionFailureHandler = new MyAccountAssertionFailureHandler(this);
            WikiClient = WikiClientFactory();
            WikiFamily = WikiFamilyFactory();
        }

        #region Infrastructures

        public string CookiesFileName { get; set; }

        private WikiClient WikiClientFactory()
        {
            var client = new WikiClient
            {
                ClientUserAgent = "Snowbush/1.0 (.NET Core; Cedarsong)",
                Timeout = TimeSpan.FromSeconds(30),
                RetryDelay = TimeSpan.FromSeconds(10),
                Logger = loggerFactory.CreateLogger("WikiClient"),
            };
            if (File.Exists(CookiesFileName))
            {
                using (var reader = File.OpenText(CookiesFileName))
                using (var jreader = new JsonTextReader(reader))
                {
                    var cookies = cookieSerializer.Deserialize<IList<Cookie>>(jreader);
                    if (cookies != null)
                    {
                        foreach (var c in cookies)
                        {
                            client.CookieContainer.Add(c);
                        }
                    }
                }
            }
            return client;
        }

        private readonly object saveSessionLock = new object();

        public void SaveSession()
        {
            lock (saveSessionLock)
            {
                using (var writer = File.CreateText(CookiesFileName))
                using (var jwriter = new JsonTextWriter(writer))
                {
                    cookieSerializer.Serialize(jwriter, WikiClient.CookieContainer.EnumAllCookies().ToList());
                }
                logger.Information("Session saved.");
            }
        }

        public static async Task LoginAsync(WikiSite site)
        {
            Console.WriteLine("=== Login to {0} ===", site);
            TRIAL:
            Console.Write("User name:");
            var userName = Console.ReadLine();
            Console.Write("Password:");
            var password = Utility.InputPassword();
            try
            {
                await site.LoginAsync(userName, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                goto TRIAL;
            }
        }

        public Task<WikiSite> GetSiteAsync(string prefix)
        {
            return GetSiteAsync(prefix, false);
        }

        public async Task<WikiSite> GetSiteAsync(string prefix, bool ensureLoggedIn)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            var site = await WikiFamily.GetSiteAsync(prefix);
            if (ensureLoggedIn && !site.AccountInfo.IsUser)
            {
                await LoginAsync(site);
                SaveSession();
            }
            return site;
        }

        #endregion

        #region Site Specific

        private WikiFamily WikiFamilyFactory()
        {
            var f = new WikiFamily(WikiClient, "Warriors")
            {
                Logger = loggerFactory.CreateLogger("WikiFamily"),
            };
            f.SiteCreated += (_, e) =>
            {
                e.Site.Logger = loggerFactory.CreateLogger("WikiSite");
                e.Site.ModificationThrottler.Logger = loggerFactory.CreateLogger("Throttler");
                e.Site.AccountAssertionFailureHandler = accountAssertionFailureHandler;
            };
            f.Register("en", "http://warriors.wikia.com/api.php");
            f.Register("zh", "http://warriors.huijiwiki.com/api.php");
            f.Register("de", "http://de.warrior-cats.wikia.com/api.php");
            return f;
        }

        #endregion

        private class MyAccountAssertionFailureHandler : IAccountAssertionFailureHandler
        {
            private readonly SiteProvider _Owner;

            public MyAccountAssertionFailureHandler(SiteProvider owner)
            {
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                _Owner = owner;
            }

            /// <inheritdoc />
            public async Task<bool> Login(WikiSite site)
            {
                await LoginAsync(site);
                _Owner.SaveSession();
                return true;
            }
        }

    }
}
