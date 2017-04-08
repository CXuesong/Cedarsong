using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Snowbush
{
    public class AllPrivateFieldsContractResolver : DefaultContractResolver
    {
        /// <inheritdoc />
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var props = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => CreateProperty(f, memberSerialization))
                .ToList();
            foreach (var p in props)
            {
                p.Writable = true;
                p.Readable = true;
            }
            return props;
        }
    }
}
