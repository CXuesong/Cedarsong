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
    public partial class Routine
    {
        public const string PageStatusFile = "pageStatus.json";
        public const string ModifiedPagesDumpFile = "modifiedPages.json";

        public readonly TimeSpan AutomaticDumpInterval = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializer serializer = JsonSerializer.Create();

        public SiteProvider SiteProvider { get; }

        /// <summary>
        /// The oldest revision allowed when getting the revisions for diff.
        /// </summary>
        public DateTime OldestRevisionTimeStamp { get; set; } = DateTime.Now - TimeSpan.FromDays(30);

        private PageStatusDictionary pageStatus;

        private readonly ObjectPool<WikitextBySectionComparer> comparerPool =
            new ObjectPool<WikitextBySectionComparer>(() => new WikitextBySectionComparer());

        private ConcurrentBag<ModifiedPageInfo> modifiedPages;

        public Routine(SiteProvider siteProvider)
        {
            SiteProvider = siteProvider;
        }

        private T LoadJson<T>(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return default(T);
            }
            PrintVerbose(this, "Load {0}", fileName);
            using (var r = File.OpenText(fileName))
            using (var jr = new JsonTextReader(r))
                return serializer.Deserialize<T>(jr);
        }

        private void SaveJson(string fileName, object content)
        {
            using (var w = File.CreateText(fileName))
            using (var jw = new JsonTextWriter(w))
                serializer.Serialize(jw, content);
            PrintVerbose(this, "Saved {0}", fileName);
        }

        private static void DeleteFile(string fileName)
        {
            File.Delete(fileName);
        }

        private void LoadSettings()
        {
            pageStatus = LoadJson<PageStatusDictionary>(PageStatusFile) ?? new PageStatusDictionary();
        }

        private void LoadModifiedPagesDump()
        {
            modifiedPages = LoadJson<ConcurrentBag<ModifiedPageInfo>>(ModifiedPagesDumpFile)
                            ?? new ConcurrentBag<ModifiedPageInfo>();
        }

        private void ClearModifiedPagesDump()
        {
            DeleteFile(ModifiedPagesDumpFile);
        }

        private void SaveSettings()
        {
            SaveJson(PageStatusFile, pageStatus);
        }

        private void SaveModifiedPagesDump()
        {
            SaveJson(ModifiedPagesDumpFile, modifiedPages);
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
            PrintMessage(this, level, message);
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
    }

    public class ModifiedPageInfo
    {
        private ModifiedPageInfo()
        {

        }

        public ModifiedPageInfo(string title, string message, float priority, Revision revision1, Revision revision2)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            Title = title;
            Message = message;
            Priority = priority;
            RevisionId1 = revision1.Id;
            RevisionTime1 = revision1.TimeStamp;
            RevisionId2 = revision2.Id;
            RevisionTime2 = revision2.TimeStamp;
            CheckedTime = DateTime.Now;
        }

        public float Priority { get; private set; }

        public string Title { get; private set; }

        public string Message { get; private set; }

        public int RevisionId1 { get; private set; }

        public DateTime RevisionTime1 { get; private set; }

        public int RevisionId2 { get; private set; }

        public DateTime RevisionTime2 { get; private set; }

        public DateTime CheckedTime { get; private set; }
    }
}
