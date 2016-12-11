using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Cloudtail
{
    public class PageStatus
    {
        public DateTime LastCheckedSections { get; set; }

    }

    public class PageStatusDictionary : Dictionary<string, PageStatus>
    {
    }
}
