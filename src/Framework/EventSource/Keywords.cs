using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Eventing
{
    public class Keywords
    {
        public const EventKeywords Task = (EventKeywords)1;
        public const EventKeywords Item = (EventKeywords)2;
        public const EventKeywords Project = (EventKeywords)3;
    }
}
