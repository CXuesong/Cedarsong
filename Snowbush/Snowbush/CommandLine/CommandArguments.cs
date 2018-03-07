using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Snowbush.CommandLine
{

    public class CommandArgument
    {
        public CommandArgument(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }

        public static explicit operator string(CommandArgument arg)
        {
            return arg?.Value;
        }

        public static explicit operator int(CommandArgument arg)
        {
            if (arg == null) throw new ArgumentNullException(nameof(arg));
            return Convert.ToInt32(arg.Value);
        }

        public static explicit operator bool(CommandArgument arg)
        {
            if (arg == null) throw new ArgumentNullException(nameof(arg));
            if (string.IsNullOrEmpty(arg.Value)) return true;
            switch (arg.Value.ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                    return false;
            }
            throw new FormatException($"Cannot convert {arg} to Boolean.");
        }

        public static explicit operator int?(CommandArgument arg)
        {
            if (arg == null) return null;
            return Convert.ToInt32(arg.Value);
        }

        public static explicit operator bool? (CommandArgument arg)
        {
            if (arg == null) return null;
            return (bool)arg;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Name == null) return Value;
            if (Value == null) return "--" + Name;
            return $"--{Name}:{Value}";
        }
    }

    public class CommandArguments
    {
        public static readonly CommandArguments Empty = new CommandArguments(Enumerable.Empty<CommandArgument>());

        public CommandArguments(IEnumerable<CommandArgument> args)
        {
            foreach (var a in args)
            {
                if (a.Name == null)
                {
                    if (positionalArguments == null)
                        positionalArguments = new List<CommandArgument>();
                    positionalArguments.Add(a);
                }
                else
                {
                    if (namedArguments == null)
                        namedArguments = new Dictionary<string, CommandArgument>(StringComparer.OrdinalIgnoreCase);
                    namedArguments.Add(a.Name, a);
                }
            }
            NamedArguments = namedArguments != null
                ? new ReadOnlyDictionary<string, CommandArgument>(namedArguments)
                : emptyDictionary;
            PositionalArguments = positionalArguments != null
                ? positionalArguments.AsReadOnly()
                : emptyList;
        }

        private static readonly IReadOnlyDictionary<string, CommandArgument> emptyDictionary
            = new ReadOnlyDictionary<string, CommandArgument>(new Dictionary<string, CommandArgument>());

        private static readonly IReadOnlyList<CommandArgument> emptyList = new CommandArgument[0];

        private readonly Dictionary<string, CommandArgument> namedArguments;
        private readonly List<CommandArgument> positionalArguments;

        public IReadOnlyDictionary<string, CommandArgument> NamedArguments { get; }

        public IReadOnlyList<CommandArgument> PositionalArguments { get; }

        public CommandArgument this[int index]
        {
            get
            {
                if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
                if (positionalArguments == null) return null;
                if (index >= positionalArguments.Count) return null;
                return positionalArguments[index];
            }
        }

        public CommandArgument this[string name]
        {
            get
            {
                if (name == null) throw new ArgumentNullException(nameof(name));
                if (namedArguments == null) return null;
                if (namedArguments.TryGetValue(name, out var a)) return a;
                return null;
            }
        }

        public int Count => NamedArguments.Count + PositionalArguments.Count;
    }
}
