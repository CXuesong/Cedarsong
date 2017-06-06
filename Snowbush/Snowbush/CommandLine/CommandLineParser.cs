using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Snowbush.CommandLine
{
    public static class CommandLineParser
    {
        private static readonly Regex NamedArgMatcher = new Regex(@"^-(?<N>[^\s:]+)(:""?(?<V>[^""]*)""?)?$");

        public static CommandArgument ParseArgument(string expr)
        {
            var match = NamedArgMatcher.Match(expr);
            if (match.Success)
            {
                return new CommandArgument(match.Groups["N"].Value, match.Groups["V"].Value);
            }
            return new CommandArgument(null, expr);
        }
    }
}
