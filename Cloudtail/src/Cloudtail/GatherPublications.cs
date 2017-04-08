using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using WikiClientLibrary;

namespace Cloudtail
{
    public class GatherPublications
    {

        private static readonly string[] Volumes =
        {
            "@Dawn of the Clans",
            "The Sun Trail",
            "Thunder Rising",
            "The First Battle",
            "The Blazing Star",
            "A Forest Divided",
            "Path of Stars",
            "@The Prophecies Begin",
            "Into the Wild",
            "Fire and Ice",
            "Forest of Secrets",
            "Rising Storm",
            "A Dangerous Path",
            "The Darkest Hour",
            "@The New Prophecy",
            "Midnight",
            "Moonrise",
            "Dawn",
            "Starlight",
            "Twilight",
            "Sunset",
            "@Power of Three",
            "The Sight",
            "Dark River",
            "Outcast",
            "Eclipse",
            "Long Shadows",
            "Sunrise",
            "@Omen of the Stars",
            "The Fourth Apprentice",
            "Fading Echoes",
            "Night Whispers",
            "Sign of the Moon",
            "The Forgotten Warrior",
            "The Last Hope",
            "@A Vision of Shadows",
            "The Apprentice's Quest",
            "Thunder and Shadow",
            "Shattered Sky",
            "Darkest Night",
            "@Super Edition",
            "Firestar's Quest",
            "Bluestar's Prophecy",
            "SkyClan's Destiny",
            "Crookedstar's Promise",
            "Yellowfang's Secret",
            "Tallstar's Revenge",
            "Bramblestar's Storm",
            "Moth Flight's Vision",
            "Hawkwing's Journey",
            "Tigerheart's Shadow",
            "@The Untold Stories",
            "Hollyleaf's Story",
            "Mistystar's Omen",
            "Cloudstar's Journey",
            "@Tales from the Clans",
            "Tigerstar's Fury",
            "Leafpool's Wish",
            "Dovewing's Silence",
            "@Shadows of the Clans",
            "Mapleshade's Vengeance",
            "Goosefeather's Curse",
            "Ravenpaw's Farewell",
            "@Legends of the Clans",
            "Spottedleaf's Heart",
            "Pinestar's Choice",
            "Thunderstar's Echo",
            "@The Field Guide",
            "Secrets of the Clans",
            "Cats of the Clans",
            "Code of the Clans",
            "Battles of the Clans",
            "Enter the Clans",
            "The Warriors Guide",
            "The Ultimate Guide",
            "@Stand-Alone Manga",
            "The Rise of Scourge",
            "@The Lost Warrior",
            "The Lost Warrior",
            "Warrior's Refuge",
            "Warrior's Return",
            "@Tigerstar and Sasha",
            "Into the Woods",
            "Escape from the Forest",
            "Return to the Clans",
            "@Ravenpaw's Path",
            "Shattered Peace",
            "A Clan in Need",
            "The Heart of a Warrior",
            "@SkyClan and the Stranger",
            "The Rescue",
            "Beyond the Code",
            "After the Flood",
        };

        public GatherPublications(SiteProvider siteProvider)
        {
            if (siteProvider == null) throw new ArgumentNullException(nameof(siteProvider));
            SiteProvider = siteProvider;
        }

        public SiteProvider SiteProvider { get; }

        private static readonly Regex PublicationEntryMatcher = new Regex(@"(?<T>.+?)\s*\((?<L>.+?)\)\s*,?\s*(?<P>.+?)\s*\((?<B>.+?)\)\s*,?\s*(?<D>[^,<]+)\s*(,\s*(?<N>.*))?");

        private IEnumerable<PublicationEntry> ParsePage(Page page)
        {
            var parser = new WikitextParser();
            var root = parser.Parse(page.Content);
            var ph = root.Lines.OfType<Heading>().FirstOrDefault(h => h.ToPlainText().Contains("Publication"));
            if (ph == null)
            {
                Logger.Cloudtail.Warn(page, "No Publication History detected");
                yield break;
            }
            var dict = new Dictionary<string, IList<string>>();
            foreach (var line in ph.EnumNextNodes().TakeWhile(n => !(n is Heading)).Cast<LineNode>())
            {
                var ls = line.ToPlainText(NodePlainTextOptions.RemoveRefTags);
                ls = ls.Trim();
                if (string.IsNullOrEmpty(ls)) continue;
                var match = PublicationEntryMatcher.Match(ls);
                if (!match.Success)
                {
                    Logger.Cloudtail.Warn(page, "Cannot parse Publication History: " + ls);
                    continue;
                }
                var entry = new PublicationEntry
                {
                    Title = page.Title,
                    Locale = match.Groups["L"].Value,
                    LocalizedTitle = match.Groups["T"].Value,
                    MediaType = match.Groups["B"].Value,
                    Publisher = match.Groups["P"].Value,
                    Note = match.Groups["N"].Value,
                };
                DateTime dt;
                if (DateTime.TryParse(match.Groups["D"].Value, out dt))
                    entry.PublishDate = dt.ToString("yyyy-MM-dd");
                else
                    entry.PublishDate = match.Groups["D"].Value;
                var r = line.Inlines.OfType<ParserTag>()
                    .FirstOrDefault(t => t.Name.Trim().Equals("ref", StringComparison.OrdinalIgnoreCase));
                if (r != null)
                {
                    if (r.Content == null)
                        entry.Cite = r.ToString();
                    else
                    {
                        var rc = parser.Parse(r.Content);
                        var link = rc.EnumDescendants().OfType<ExternalLink>().FirstOrDefault();
                        if (link != null)
                            entry.Cite = link.Target.ToPlainText();
                        else
                            entry.Cite = r.ToString();
                    }
                }
                else
                {
                    var rt = line.Inlines.OfType<Template>().Where(
                        t => t.Name.ToPlainText().Trim().Equals("R", StringComparison.OrdinalIgnoreCase)).ToArray();
                    entry.Cite = string.Join("", rt.Select(t => t.ToString()));
                }
                yield return entry;
            }
        }

        public async Task FetchAsync()
        {
            var enSite = SiteProvider.EnSite;
            var pages = Volumes.Where(v => !v.StartsWith("@")).Select(t => Page.FromTitle(enSite, t)).ToArray();
            Logger.Cloudtail.Info(this, "Fetching {0} pages…", pages.Length);
            await pages.RefreshAsync(PageQueryOptions.FetchContent);
            var pubList = new List<PublicationEntry>();
            foreach (var p in pages)
            {
                var p1 = p;
                Logger.Cloudtail.Trace(p1, "Parsing…");
                var parsed = ParsePage(p1).ToArray();
                if (parsed.Length > 0)
                {
                    pubList.AddRange(parsed);
                } else {
                    Logger.Cloudtail.Info(this, "Disambiguate: {0}…", p1);
                    p1 = new Page(enSite, p1.Title + " (Book)");
                    await p1.RefreshAsync(PageQueryOptions.FetchContent);
                    if (p1.Exists) pubList.AddRange(ParsePage(p1));
                }
            }
            SavePublicationList(pubList);
        }

        private static readonly Regex TranslatorMatcher = new Regex(@"translated by\s*(.+?)(,|$)");

        private static readonly Regex NarratorMatcher = new Regex(@"narrated by\s*(.+?)(,|$)");

        public async Task ExportModulesAsync()
        {
            await Task.Yield();
            var pubList = LoadPublicationList().ToLookup(p => p.Locale);
            var site = SiteProvider.ZhSite;
            foreach (var lang in pubList)
            {
                if (lang.Key == "en") continue;
                var isFirst = true;
                //using (var sw = File.CreateText("publication-" + lang.Key + ".txt"))
                using (var sw = new StringWriter())
                {
                    Action<string, string, bool> AddProperty = (name, value, optional) =>
                    {
                        if (optional && string.IsNullOrEmpty(value)) return;
                        sw.Write(new string(' ', 12));
                        sw.Write(string.Format("{0, -12}", name));
                        sw.Write("= ");
                        if (string.IsNullOrEmpty(value))
                        {
                            sw.Write("nil");
                        }
                        else
                        {
                            sw.Write('"');
                            sw.Write(value.Replace("\\", "\\\\").Replace("\"", ""));
                            sw.Write('"');
                        }
                        sw.WriteLine(",");
                    };
                    var culture = new CultureInfo(lang.Key);
                    sw.WriteLine("-- Publication information for " + culture.EnglishName + " [" + culture.NativeName + "].");
                    sw.WriteLine();
                    sw.WriteLine("local data = {");
                    foreach (var vol in Volumes)
                    {
                        if (vol.StartsWith("@"))
                        {
                            if (!isFirst) sw.WriteLine();
                            sw.WriteLine("    -- " + vol.Substring(1));
                            continue;
                        }
                        isFirst = false;
                        sw.WriteLine(string.Format("    [\"{0}\"] = {{", vol));
                        var pubs = lang.Where(e => e.Title == vol).ToList();
                        if (pubs.Any())
                        {
                            foreach (var pub in pubs)
                            {
                                if (pub.MediaType?.Contains("unknow") == true) pub.MediaType = null;
                                string translator = null, narrator = null;
                                if (!string.IsNullOrWhiteSpace(pub.Note))
                                {
                                    translator = TranslatorMatcher.Match(pub.Note).Groups[1].Value;
                                    narrator = NarratorMatcher.Match(pub.Note).Groups[1].Value;
                                    if (string.IsNullOrEmpty(translator) && string.IsNullOrEmpty(narrator))
                                    {
                                        Logger.Cloudtail.Warn(pub.LocalizedTitle, "Cannot parse: {0}", pub.Note);
                                    }
                                }
                                sw.WriteLine("        {");
                                AddProperty("title", pub.LocalizedTitle, false);
                                AddProperty("pubDate", pub.PublishDate, false);
                                AddProperty("publisher", pub.Publisher, false);
                                AddProperty("mediaType", pub.MediaType.ToLower(), false);
                                AddProperty("translator", translator, true);
                                AddProperty("narrator", narrator, true);
                                AddProperty("illustrator", null, false);
                                AddProperty("isbn", null, false);
                                AddProperty("ref", pub.Cite, false);
                                sw.WriteLine("        },");
                            }
                        }
                        sw.WriteLine("    },");
                    }
                    sw.WriteLine("}");
                    sw.WriteLine();
                    sw.WriteLine("return data");
                    var page = new Page(site, "Module:Publication/data/" + lang.Key);
                    page.Content = sw.ToString();
                    Logger.Cloudtail.Info(this, "Writting {0}", culture);
                    await page.UpdateContentAsync("机器人：从[[:en:]]导入出版信息（" + culture.DisplayName + "）。");
                }
            }
        }

        private void SavePublicationList(IEnumerable<PublicationEntry> pe)
        {
            using (var sw = File.CreateText("publicationList.txt"))
            {
                foreach (var entry in pe)
                {
                    sw.WriteLine(string.Join("\t",
                        entry.Title, entry.LocalizedTitle,
                        entry.Locale, entry.MediaType, entry.PublishDate, entry.Publisher,
                        entry.Note, entry.Cite
                    ));
                }
            }
        }

        private IEnumerable<PublicationEntry> LoadPublicationList()
        {
            foreach (var l in File.ReadAllLines("publicationList.txt"))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                var f = l.Split('\t');
                yield return new PublicationEntry
                {
                    Title = f[0],
                    LocalizedTitle = f[1],
                    Locale = f[2],
                    MediaType = f[3],
                    PublishDate = f[4],
                    Publisher = f[5],
                    Note = f[6],
                    Cite = f[7],
                };
            }
        }

        private class PublicationEntry
        {
            public string Title { get; set; }

            public string Locale { get; set; }

            public string LocalizedTitle { get; set; }

            public string Publisher { get; set; }

            public string PublishDate { get; set; }

            public string MediaType { get; set; }

            public string Note { get; set; }

            public string Cite { get; set; }
        }

    }
}
