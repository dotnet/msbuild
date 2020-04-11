using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework
{
    public class SdkResultItem
    {
        public string ItemSpec { get; set; }
        public Dictionary<string, string> Metadata { get;}

        public SdkResultItem()
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public SdkResultItem(string itemSpec, Dictionary<string, string> metadata)
        {
            ItemSpec = itemSpec;
            Metadata = metadata;
        }
    }
}
