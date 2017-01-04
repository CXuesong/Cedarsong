using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WikiClientLibrary;
using WikiClientLibrary.Client;

namespace Cloudtail
{
    public class SiteProvider
    {
        private readonly Lazy<WikiClient> _WikiClient;

        private static readonly JsonSerializer cookieSerializer = new JsonSerializer
        {
            ContractResolver = new AllPrivateFieldsContractResolver()
        };

        public SiteProvider(string cookiesFileName)
        {
            CookiesFileName = cookiesFileName;
            _WikiClient = new Lazy<WikiClient>(WikiClientFactory);
            _EnSite = new Lazy<Site>(() => CreateSite(EnApiEntryPoint, "EnSite"));
            _ZhSite = new Lazy<Site>(() => CreateSite(ZhApiEntryPoint, "ZhSite"));
        }

        public SiteProvider() : this(null)
        {

        }

        #region Infrastructures

        public string CookiesFileName { get; set; }

        public WikiClient WikiClient => _WikiClient.Value;

        private WikiClient WikiClientFactory()
        {
            var client = new WikiClient { ClientUserAgent = "Cloudtail/1.0 (.NET Core; Cedarsong)" };
            client.Logger = new TraceLogger("Client");
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

        public void SaveSession()
        {
            if (_WikiClient.IsValueCreated)
            {
                using (var writer = File.CreateText(CookiesFileName))
                using (var jwriter = new JsonTextWriter(writer))
                {
                    cookieSerializer.Serialize(jwriter, WikiClient.CookieContainer.EnumAllCookies().ToList());
                }
                Console.WriteLine("Session saved.");
            }
        }

        #endregion

        #region Site Specific

        public const string EnApiEntryPoint = "http://warriors.wikia.com/api.php";
        public const string ZhApiEntryPoint = "http://warriors.huiji.wiki/api.php";

        private readonly Lazy<Site> _EnSite, _ZhSite;

        private Site CreateSite(string entryPoint, string name)
        {
            var site = Site.CreateAsync(WikiClient, entryPoint).Result;
            site.Logger = new TraceLogger(name);
            UI.Print("{0}@{1}", site.UserInfo.Name, site.SiteInfo.SiteName);
            return site;
        }

        public Site EnSite => _EnSite.Value;

        public Site ZhSite => _ZhSite.Value;

        public void EnsureLoggedIn(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (site.UserInfo.IsUser) return;
            UI.Print("Login to {0}。", (object)site.SiteInfo.SiteName);
            TRIAL:
            var userName = UI.Input("User name");
            var password = UI.InputPassword("Password");
            UI.Print("…");
            try
            {
                site.LoginAsync(userName, password).Wait();
            }
            catch (Exception ex)
            {
                UI.PrintError(ex);
                goto TRIAL;
            }
            SaveSession();
            Debug.Assert(site.UserInfo.IsUser);
        }

        #endregion

        private class TraceLogger : ILogger
        {
            public TraceLogger(string sourceName)
            {
                SourceName = sourceName;
            }

            public string SourceName { get; }

            /// <inheritdoc />
            public void Trace(string message)
            {
                Logger.Wcl.Trace(SourceName, "{0}", message);
            }

            /// <inheritdoc />
            public void Info(string message)
            {
                Logger.Wcl.Info(SourceName, "{0}", message);
            }

            /// <inheritdoc />
            public void Warn(string message)
            {
                Logger.Wcl.Warn(SourceName, "{0}", message);
            }

            /// <inheritdoc />
            public void Error(Exception exception, string message)
            {
                Logger.Wcl.Exception(SourceName, exception);
            }
        }

    }
}
