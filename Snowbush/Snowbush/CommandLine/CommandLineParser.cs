using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Snowbush.CommandLine
{
    public static class CommandLineParser
    {
        private static readonly Regex NamedArgMatcher = new Regex(@"^-(?<N>[^\s:]+)(:""?(?<V>[^""]*)""?)?$");

        public static (string Key, string Value) ParseArgument(string expr)
        {
            var match = NamedArgMatcher.Match(expr);
            if (match.Success) return (match.Groups["N"].Value, match.Groups["V"].Value);
            return (null, expr);
        }
    }
}
