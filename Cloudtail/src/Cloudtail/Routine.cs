using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using System.Threading.Tasks.Dataflow;
using WikiDiffSummary;
using System.Reactive.Linq;

namespace Cloudtail
{
    public class Routine
    {
        public const string EnApiEntryPoint = "http://warriors.wikia.com/api.php";
        public const string ZhApiEntryPoint = "http://warriors.huiji.wiki/api.php";
        public const string PageStatusFile = "pageStatus.json";

        private static readonly JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });

        public WikiClient Client { get; } = new WikiClient();

        /// <summary>
        /// The oldest revision allowed when getting the revisions for diff.
        /// </summary>
        public DateTime OldestRevisionTimeStamp { get; set; } = DateTime.Now - TimeSpan.FromDays(30);

        private Site siteEn;
        private PageStatusDictionary pageStatus;

        private readonly ObjectPool<WikitextBySectionComparer> comparerPool =
            new ObjectPool<WikitextBySectionComparer>(() => new WikitextBySectionComparer());

        private readonly ConcurrentBag<ModifiedPageInfo> modifiedPages = new ConcurrentBag<ModifiedPageInfo>();

        public Routine()
        {

        }

        private void LoadSettings()
        {
            if (!File.Exists(PageStatusFile))
            {
                pageStatus = new PageStatusDictionary();
                return;
            }
            using (var r = File.OpenText(PageStatusFile))
            using (var jr = new JsonTextReader(r))
                pageStatus = serializer.Deserialize<PageStatusDictionary>(jr);
        }

        private void SaveSettings()
        {
            using (var w = File.CreateText(PageStatusFile))
            using (var jw = new JsonTextWriter(w))
                serializer.Serialize(jw, pageStatus);
        }
        private void PrintVerbose(string message)
        {
            PrintMessage(message, TraceLevel.Verbose);
        }

        private void PrintVerbose(object source, string message)
        {
            PrintMessage(source, TraceLevel.Verbose, message);
        }

        private void PrintVerbose(object source, string format, params object[] args)
        {
            PrintMessage(source, TraceLevel.Verbose, format, args);
        }

        private void PrintInfo(string message)
        {
            PrintMessage(message, TraceLevel.Info);
        }

        private void PrintInfo(object source, string message)
        {
            PrintMessage(source, TraceLevel.Info, message);
        }

        private void PrintInfo(object source, string format, params object[] args)
        {
            PrintMessage(source, TraceLevel.Info, format, args);
        }

        private void PrintMessage(object source, TraceLevel level, string format, params object[] args)
        {
            PrintMessage(source, level, string.Format(format, args));
        }

        private void PrintMessage(string message, TraceLevel level)
        {
            PrintMessage(null, level, message);
        }

        private static readonly object ConsoleLock = new object();

        private void PrintMessage(object source, TraceLevel level, string message)
        {
            lock (ConsoleLock)
            {
                switch (level)
                {
                    case TraceLevel.Verbose:
                        break;
                    case TraceLevel.Info:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case TraceLevel.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case TraceLevel.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                }
                Console.WriteLine(source + ":" + message);
                Console.ResetColor();
            }
        }

        public async Task PerformAsync()
        {
            LoadSettings();
            siteEn = await Site.CreateAsync(Client, EnApiEntryPoint);
            //var siteZh = await Site.CreateAsync(client, ZhApiEntryPoint);
            var gen = new CategoryMembersGenerator(siteEn, "Characters") {PagingSize = 20};
            PrintInfo("Checking category…");
            var pagesSource = gen.EnumPagesAsync().Take(50).ToObservable();
            var sourceBlock = pagesSource.ToSourceBlock();
            var processorBlock = new ActionBlock<Page>(CheckPage);
            using (sourceBlock.LinkTo(processorBlock, new DataflowLinkOptions {PropagateCompletion = true}))
            {
                await processorBlock.Completion;
            }
            // 
            while (true)
            {
                ModifiedPageInfo p;
                if (!modifiedPages.TryTake(out p)) break;

            }
            // Cleanup
            comparerPool.Clear();
            Debug.Assert(modifiedPages.Count == 0);
        }

        private async Task CheckPage(Page page)
        {
            Debug.Assert(page != null);
            if (page.IsRedirect)
            {
                PrintInfo(page, "Ignoring redirect.");
                return;
            }
            PrintVerbose(page, "Checking page.");
            var status = pageStatus.TryGetValue(page.Title);
            if (status == null)
            {
                status = new PageStatus();
                pageStatus.Add(page.Title, status);
            }
            if (status.LastCheckedSections < OldestRevisionTimeStamp)
                status.LastCheckedSections = OldestRevisionTimeStamp;
            // The page has been modified since last visit.
            if (page.LastRevision.TimeStamp > status.LastCheckedSections)
            {
                await CheckSections(page);
            }
        }

        private async Task CheckSections(Page page)
        {
            Debug.Assert(page != null);
            PrintVerbose(page, "Checking revisions.");
            var status = pageStatus[page.Title];
            var rev1gen = new RevisionGenerator(page) {StartTime = status.LastCheckedSections, PagingSize = 1};
            var rev2gen = new RevisionGenerator(page) {PagingSize = 1};
            var revs = await Task.WhenAll(rev1gen.EnumRevisionsAsync(PageQueryOptions.FetchContent).FirstOrDefault(),
                rev2gen.EnumRevisionsAsync(PageQueryOptions.FetchContent).FirstOrDefault());
            var rev1 = revs[0];
            var rev2 = revs[1];
            var cmp = comparerPool.Get();
            var diff = cmp.Compare(rev1.Content, rev2.Content);
            comparerPool.Put(cmp);
            var fdiff = FormatDiff(diff);
            if (fdiff == null) return;
            PrintInfo(page, "Change detected,\n{0}", fdiff.Item1);
            modifiedPages.Add(new ModifiedPageInfo(page.Title, fdiff.Item1, fdiff.Item2));
        }

        // <User-friendly wikitext output, added characters>
        private Tuple<string, int> FormatDiff(IEnumerable<SectionDiff> diffs)
        {
            var sb = new StringBuilder();
            var addedChars = 0;
            var anyChanges = false;
            Func<SectionPath, bool> Interesting = path => path == SectionPath.Empty || path.Contains("Trivia");
            foreach (var d in diffs)
            {
                if (d.Status == SectionDiffStatus.Identical) continue;
                if (d.Section1 != null && Interesting(d.Section1.Path)
                    || d.Section2 != null && Interesting(d.Section2.Path))
                {
                    anyChanges = true;
                    switch (d.Status)
                    {
                        case SectionDiffStatus.Added:
                            sb.AppendLine(";" + FormatSectionPath(d.Section2.Path));
                            sb.Append(":New section. ");
                            break;
                        case SectionDiffStatus.Removed:
                            sb.AppendLine(";" + FormatSectionPath(d.Section2.Path));
                            sb.Append(":Removed section. ");
                            break;
                        case SectionDiffStatus.WhitespaceModified:
                        case SectionDiffStatus.Modified:
                            Debug.Assert(d.Section1 != null);
                            sb.AppendLine(";" + FormatSectionPath(d.Section1.Path));
                            sb.Append(":");
                            if (d.Section1.Path != d.Section2.Path)
                                sb.Append("Renamed to '''" + FormatSectionPath(d.Section2.Path) + "'''. ");
                            if (d.Status == SectionDiffStatus.WhitespaceModified)
                                sb.Append("Whitespace modified.");
                            break;
                    }
                    if (d.AddedChars > 0 || d.RemovedChars > 0)
                    {
                        sb.Append("( ");
                        if (d.AddedChars > 0) sb.Append("+" + d.AddedChars + " ");
                        if (d.RemovedChars > 0) sb.Append("-" + d.RemovedChars + " ");
                        sb.Append(')');
                    }
                    addedChars += d.AddedChars;
                }
            }
            if (!anyChanges) return null;
            return Tuple.Create(sb.ToString(), addedChars);
        }

        private string FormatSectionPath(SectionPath path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path == SectionPath.Empty) return "[Leading]";
            return path.ToString();
        }
    }

    public class ModifiedPageInfo
    {
        public ModifiedPageInfo(string title, string message, int priority)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            Title = title;
            Message = message;
            Priority = priority;
        }

        public int Priority { get; }

        public string Title { get; }

        public string Message { get; }
    }
}
