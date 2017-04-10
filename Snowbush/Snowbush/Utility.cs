using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using WikiClientLibrary;

namespace Snowbush
{
    internal static class Utility
    {
        public static IEnumerable<Cookie> EnumAllCookies(this CookieContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            var domainTable = (IDictionary)typeof(CookieContainer)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(f => f.Name.Contains("domainTable"))
                .GetValue(container);
            foreach (DictionaryEntry entry in domainTable)
            {
                var pathList_GetEnumerator = entry.Value.GetType().GetMethod("GetEnumerator");
                var e = (IEnumerator)pathList_GetEnumerator.Invoke(entry.Value, null);
                while (e.MoveNext())
                {
                    var p = (KeyValuePair<string, CookieCollection>)e.Current;
                    foreach (Cookie c in p.Value)
                    {
                        yield return c;
                    }
                }
            }
        }

        private static readonly MethodInfo CookieContainer_Add = typeof(CookieContainer)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(m =>
            {
                if (m.Name != "Add") return false;
                var p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType == typeof(Cookie) && p[1].ParameterType == typeof(bool);
            });

        public static void Add(this CookieContainer container, Cookie cookie)
        {
            if (cookie == null)
                throw new ArgumentNullException(nameof(cookie));

            if (cookie.Domain.Length == 0)
                throw new ArgumentException("cookie.Domain is empty.");

            // new_cookie.VerifySetDefaults(new_cookie.Variant, uri, IsLocalDomain(uri.Host), m_fqdnMyDomain, true, true);

            CookieContainer_Add.Invoke(container, new object[] { cookie, true });
        }

        public static ISourceBlock<T> ToSourceBlock<T>(this IObservable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var buffer = new BufferBlock<T>();
            source.Subscribe(i => buffer.Post(i), () => buffer.Complete());
            return buffer;
        }

        public static string InputPassword()
        {
            var pass = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return pass.ToString();
                    case ConsoleKey.Backspace:
                        if (pass.Length > 0) pass.Remove(pass.Length - 1, 1);
                        break;
                    case ConsoleKey.Escape:
                        pass.Clear();
                        break;
                    default:
                        if (key.KeyChar != '\0') pass.Append(key.KeyChar);
                        break;
                }
            }
        }

        private static readonly object consoleLock = new object();

        public static void ShowDiff(string text1, string text2)
        {
            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(text1, text2);
            int counter1 = 0, counter2 = 0;
            lock (consoleLock)
            {
                foreach (var line in diff.Lines)
                {
                    Debug.Assert(line.Type != ChangeType.Imaginary);
                    switch (line.Type)
                    {
                        case ChangeType.Inserted:
                            counter2++;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("{0,4}|{1,4}+ ", counter1, counter2);
                            Console.WriteLine(line.Text);
                            break;
                        case ChangeType.Deleted:
                            counter1++;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("{0,4}|{1,4}- ", counter1, counter2);
                            Console.WriteLine(line.Text);
                            break;
                        default:
                            counter1++;
                            counter2++;
                            //Console.ForegroundColor = ConsoleColor.White;
                            //Console.Write("  ");
                            break;
                    }
                }
                Console.ResetColor();
            }
        }
    }
}
