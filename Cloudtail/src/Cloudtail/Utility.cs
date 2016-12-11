using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Reactive.Linq;

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
    }
}
