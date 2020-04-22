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

        public override bool Equals(object obj)
        {
            if (obj is SdkResultItem item &&
                   ItemSpec == item.ItemSpec &&
                   Metadata?.Count == item.Metadata?.Count)
            {
                if (Metadata != null)
                {
                    foreach (var kvp in Metadata)
                    {
                        if (item.Metadata[kvp.Key] != kvp.Value)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = -849885975;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ItemSpec);

            if (Metadata != null)
            {
                foreach (var kvp in Metadata)
                {
                    hashCode = hashCode * 1521134295 + kvp.Key.GetHashCode();
                    hashCode = hashCode * 1521134295 + kvp.Value.GetHashCode();
                }
            }

            return hashCode;
        }
    }
}
