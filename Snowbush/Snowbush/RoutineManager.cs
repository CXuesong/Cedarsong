using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Snowbush
{
    public class RoutineManager
    {

        private class RoutineEntry
        {
            public RoutineEntry(string name, Type routineType)
            {
                Name = name;
                RoutineType = routineType;
            }

            public Type RoutineType { get; }

            public string Name { get; }
        }

        private readonly Dictionary<Type, RoutineEntry> routineDict = new Dictionary<Type, RoutineEntry>();
        private readonly Dictionary<string, RoutineEntry> nameRoutineDict = new Dictionary<string, RoutineEntry>(StringComparer.OrdinalIgnoreCase);

        public void Register(Type routineType)
        {
            if (routineType == null) throw new ArgumentNullException(nameof(routineType));
            if (!typeof(IRoutine).IsAssignableFrom(routineType)) throw new ArgumentException("Routine class should implement IRoutine.", nameof(routineType));
            var name = routineType.Name;
            if (name.EndsWith("Routine", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - 7);
            var entry = new RoutineEntry(name, routineType);
            if (routineDict.ContainsKey(routineType)) throw new InvalidOperationException($"Routine: {routineType} has already been registered.");
            if (nameRoutineDict.ContainsKey(name)) throw new InvalidOperationException($"Routine name: {name} has already been registered.");
            routineDict.Add(routineType, entry);
            nameRoutineDict.Add(name, entry);
        }

        public void Register(IEnumerable<Type> routineTypes)
        {
            if (routineTypes == null) return;
            foreach (var t in routineTypes)
            {
                Register(t);
            }
        }

        public Type TryResolve(string routineName)
        {
            if (nameRoutineDict.TryGetValue(routineName, out var entry))
                return entry.RoutineType;
            return Type.GetType(routineName, false, true);
        }

        public Type Resolve(string routineName)
        {
            return TryResolve(routineName) ??
                   throw new KeyNotFoundException($"Cannot resolve routine: {routineName} .");
        }

        public IEnumerable<Type> GetRegisteredRoutineTypes() => routineDict.Keys;

        public string GetRoutineName(Type routineType) => routineDict[routineType].Name;
    }
}
