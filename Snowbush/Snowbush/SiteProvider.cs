using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog.Core;
using WikiClientLibrary;
using WikiClientLibrary.Client;

namespace Snowbush
{
    public class SiteProvider
    {
        public WikiClient WikiClient { get; }

        public Family WikiFamily { get; }

        private readonly Serilog.ILogger logger;

        private readonly SerilogWclLogger wclLogger;

        private static readonly JsonSerializer cookieSerializer = new JsonSerializer
        {
            ContractResolver = new AllPrivateFieldsContractResolver()
        };

        public SiteProvider(string cookiesFileName, Serilog.ILogger logger)
        {
            this.logger = logger.ForContext<SiteProvider>();
            wclLogger = new SerilogWclLogger(logger);
            CookiesFileName = cookiesFileName;
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
                ThrottleTime = TimeSpan.FromSeconds(2)
            };
            client.Logger = wclLogger;
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

        public static async Task LoginAsync(Site site)
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

        public Task<Site> GetSiteAsync(string prefix)
        {
            return GetSiteAsync(prefix, false);
        }

        public async Task<Site> GetSiteAsync(string prefix, bool ensureLoggedIn)
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

        private Family WikiFamilyFactory()
        {
            var f = new MyFamily(this, "Warriors");
            f.Logger = wclLogger;
            f.Register("en", "http://warriors.wikia.com/api.php");
            f.Register("zh", "http://warriors.huiji.wiki/api.php");
            return f;
        }

        #endregion

        private class SerilogWclLogger : ILogger
        {
            public Serilog.ILogger UnderlyingLogger { get; }

            public SerilogWclLogger(Serilog.ILogger logger)
            {
                if (logger == null) throw new ArgumentNullException(nameof(logger));
                UnderlyingLogger = logger;
            }

            /// <inheritdoc />
            public void Trace(object source, string message)
            {
                UnderlyingLogger.Debug("{source}: {message}", source, message);
            }

            /// <inheritdoc />
            public void Info(object source, string message)
            {
                UnderlyingLogger.Information("{source}: {message}", source, message);
            }

            /// <inheritdoc />
            public void Warn(object source, string message)
            {
                UnderlyingLogger.Warning("{source}: {message}", source, message);
            }

            /// <inheritdoc />
            public void Error(object source, Exception exception, string message)
            {
                UnderlyingLogger.Error(exception, "{source}: {message}", source, message);
            }
        }

        private class MyFamily : Family
        {
            private readonly IAccountAssertionFailureHandler accountAssertionFailureHandler;

            /// <inheritdoc />
            public MyFamily(SiteProvider owner, string name) : base(owner.WikiClient, name)
            {
                accountAssertionFailureHandler = new MyAccountAssertionFailureHandler(owner);
            }

            /// <inheritdoc />
            protected override async Task<Site> CreateSiteAsync(string prefix, string apiEndpoint)
            {
                var site = await base.CreateSiteAsync(prefix, apiEndpoint);
                site.AccountAssertionFailureHandler = accountAssertionFailureHandler;
                Logger?.Info(this, $"{site.AccountInfo.Name}@{site.SiteInfo.SiteName}");
                return site;
            }
        }

        private class MyAccountAssertionFailureHandler : IAccountAssertionFailureHandler
        {
            private readonly SiteProvider _Owner;

            public MyAccountAssertionFailureHandler(SiteProvider owner)
            {
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                _Owner = owner;
            }

            /// <inheritdoc />
            public async Task<bool> Login(Site site)
            {
                await LoginAsync(site);
                _Owner.SaveSession();
                return true;
            }
        }

    }
}
