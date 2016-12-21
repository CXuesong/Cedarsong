using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using WikiClientLibrary;
using WikiClientLibrary.Generators;
using WikiDiffSummary;

namespace Cloudtail
{
    partial class Routine
    {
        private volatile int CheckedPagesCount;

        public async Task PerformAsync()
        {
            CheckedPagesCount = 0;
            LoadSettings();
            LoadModifiedPagesDump();
            //var siteZh = await Site.CreateAsync(client, ZhApiEntryPoint);
            var gen = new CategoryMembersGenerator(SiteProvider.EnSite, "Characters")
            {
                PagingSize = 100,
                MemberTypes = CategoryMemberTypes.Page
            };
            Logger.Cloudtail.Info(this, "Checking category…");
            var pagesSource = gen.EnumPagesAsync().ToObservable();
            var sourceBlock = pagesSource.ToSourceBlock();
            var processorBlock = new ActionBlock<Page>(CheckPage);
            using (sourceBlock.LinkTo(processorBlock, new DataflowLinkOptions {PropagateCompletion = true}))
            {
                using (var cts = new CancellationTokenSource())
                {
                    var d = AutoDumpStatus(cts.Token);
                    cts.Cancel();
                }
                await processorBlock.Completion;
            }
            // 
            if (modifiedPages.Count > 0)
            {
                var report = GenerateReport();
                SiteProvider.EnsureLoggedIn(SiteProvider.ZhSite);
                var page = new Page(SiteProvider.ZhSite, "Project:Cloudtail/Sandbox");
                page.Content = report;
                Logger.Cloudtail.Trace(this, "Writing report…");
                await page.UpdateContentAsync("更新差异报告。", false, true);
                Logger.Cloudtail.Info(page, "Report saved.");
            }
            // Cleanup
            SaveSettings();
            ClearModifiedPagesDump();
            modifiedPages = null;
            comparerPool.Clear();
        }

        private async Task CheckPage(Page page)
        {
            Debug.Assert(page != null);
            if (page.IsRedirect)
            {
                Logger.Cloudtail.Warn(page, "Ignoring redirect.");
                return;
            }
            var counter = Interlocked.Increment(ref CheckedPagesCount);
            Logger.Cloudtail.Trace(page, "Checking page #{0}.", counter);
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
                status.LastCheckedSections = DateTime.Now;
            }
        }

        private async Task AutoDumpStatus(CancellationToken ct)
        {
            var lastCollectionCount = modifiedPages.Count;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(AutomaticDumpInterval, ct);
                if (modifiedPages.Count - lastCollectionCount > 3)
                {
                    SaveModifiedPagesDump();
                    SaveSettings();
                    Logger.Cloudtail.Info(this, "Automatic dump has been written.");
                    lastCollectionCount = modifiedPages.Count;
                }
            }
        }

        private async Task CheckSections(Page page)
        {
            Debug.Assert(page != null);
            Logger.Cloudtail.Trace(page, "Checking revisions.");
            var status = pageStatus[page.Title];
            var rev1gen = new RevisionGenerator(page) { StartTime = status.LastCheckedSections, PagingSize = 1 };
            var rev2gen = new RevisionGenerator(page) { PagingSize = 1 };
            var revs = await Task.WhenAll(rev1gen.EnumRevisionsAsync(PageQueryOptions.FetchContent).FirstOrDefault(),
                rev2gen.EnumRevisionsAsync(PageQueryOptions.FetchContent).FirstOrDefault());
            var rev1 = revs[0];
            var rev2 = revs[1];
            var cmp = comparerPool.Get();
            var diff = cmp.Compare(rev1.Content, rev2.Content);
            comparerPool.Put(cmp);
            var fdiff = FormatDiff(diff);
            if (fdiff == null) return;
            Logger.Cloudtail.Info(page, "Change detected,\n{0}", fdiff.Item1);
            modifiedPages.Add(new ModifiedPageInfo(page.Title, fdiff.Item1, fdiff.Item2, rev1, rev2));
        }

        private Tuple<string, float> FormatDiff(IEnumerable<SectionDiff> diffs)
        {
            var sb = new StringBuilder();
            var priority = 0f;
            var anyChanges = false;
            Func<SectionPath, float> PriorityFactor = path =>
            {
                if (path == SectionPath.Empty) return 1;
                if (path.Contains("Trivia")) return 2;
                return -1;
            };
            foreach (var d in diffs)
            {
                if (d.Status == SectionDiffStatus.Identical) continue;
                var pf = PriorityFactor(d.Section1?.Path ?? d.Section2?.Path);
                if (pf > 0)
                {
                    anyChanges = true;
                    switch (d.Status)
                    {
                        case SectionDiffStatus.Added:
                            sb.AppendLine(";" + FormatSectionPath(d.Section2.Path));
                            sb.Append(":新小节。");
                            break;
                        case SectionDiffStatus.Removed:
                            sb.AppendLine(";" + FormatSectionPath(d.Section2.Path));
                            sb.Append(":小节被移除。");
                            break;
                        case SectionDiffStatus.WhitespaceModified:
                        case SectionDiffStatus.Modified:
                            Debug.Assert(d.Section1 != null);
                            sb.AppendLine(";" + FormatSectionPath(d.Section1.Path));
                            sb.Append(":");
                            if (d.Section1.Path != d.Section2.Path)
                                sb.Append("重命名为'''" + FormatSectionPath(d.Section2.Path) + "'''。");
                            if (d.Status == SectionDiffStatus.WhitespaceModified)
                                sb.Append("空白变化。");
                            break;
                    }
                    if (d.AddedChars > 0 || d.RemovedChars > 0)
                    {
                        sb.Append("( ");
                        if (d.AddedChars > 0) sb.Append("+" + d.AddedChars + " ");
                        if (d.RemovedChars > 0) sb.Append("-" + d.RemovedChars + " ");
                        sb.Append(')');
                    }
                    sb.AppendLine();
                    priority += d.AddedChars*pf;
                }
            }
            if (!anyChanges) return null;
            return Tuple.Create(sb.ToString(), priority);
        }

        private string FormatSectionPath(SectionPath path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path == SectionPath.Empty) return "[导语]";
            return path.ToString();
        }

        private string GenerateReport()
        {
            var builder = new StringBuilder();
            var ordered = modifiedPages.OrderByDescending(p => p.Priority);
            builder.AppendFormat("已经处理{0}个页面，其中有{1}个页面发生了显著变化。报告生成于{2}。\n\n",
                CheckedPagesCount, modifiedPages.Count, DateTime.Now);
            foreach (var p in ordered)
            {
                builder.AppendFormat("== [[:en:{0}]] ==\n", p.Title);
                builder.AppendLine("<small>");
                builder.AppendFormat(":检查于：{0}\n:版本1：{1}\n:版本2：{2}\n", p.CheckedTime, p.RevisionTime1, p.RevisionTime2);
                builder.AppendFormat(":[{{{{Diff|:en:{0}|{2}|{1}|显示差异}}}}]", p.Title, p.RevisionId1, p.RevisionId2);
                builder.AppendLine("</small>");
                builder.AppendLine();
                builder.Append(p.Message.TrimEnd());
                builder.AppendLine();
                builder.AppendLine();
            }
            builder.AppendLine();
            builder.Append("__NOEDITSECTION__\n");
            return builder.ToString();
        }
    }
}