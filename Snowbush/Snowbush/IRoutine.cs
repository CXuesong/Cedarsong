using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Snowbush.CommandLine;

namespace Snowbush
{
    public interface IRoutine
    {
        Task PerformAsync(CommandArguments arguments);
    }
}
