using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Snowbush
{
    public interface IRoutine
    {
        Task PerformAsync(IList<(string Key, string Value)> arguments);
    }
}
