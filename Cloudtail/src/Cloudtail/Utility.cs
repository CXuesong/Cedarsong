using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;

namespace Cloudtail
{
    public static class Utility
    {
        public static TValue TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            return TryGetValue<TKey, TValue>(dict, key, default(TValue));
        }

        public static TValue TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            TValue value;
            if (dict.TryGetValue(key, out value)) return value;
            return defaultValue;
        }

        public static ISourceBlock<T> ToSourceBlock<T>(this IObservable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var buffer = new BufferBlock<T>();
            source.Subscribe(i => buffer.Post(i), () => buffer.Complete());
            return buffer;
        }

        public static IEnumerable<Cookie> EnumAllCookies(this CookieContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            var domainTable = (IDictionary) typeof(CookieContainer)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(f => f.Name.Contains("domainTable"))
                .GetValue(container);
            foreach (DictionaryEntry entry in domainTable)
            {
                var pathList_GetEnumerator = entry.Value.GetType().GetMethod("GetEnumerator");
                var e = (IEnumerator) pathList_GetEnumerator.Invoke(entry.Value, null);
                while (e.MoveNext())
                {
                    var p = (KeyValuePair<string, CookieCollection>) e.Current;
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

            CookieContainer_Add.Invoke(container, new object[] {cookie, true});
        }
    }
}
