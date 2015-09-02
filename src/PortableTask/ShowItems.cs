using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PortableTask
{
    public class ShowItems : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            if (Items == null)
            {
                Log.LogError("Items was null");
            }
            else if (Items.Length == 0)
            {
                Log.LogMessage("No Items");
            }
            else
            {
                foreach (ITaskItem item in Items)
                {
                    Log.LogMessage(item.ItemSpec);
                }
            }


            
            return true;
        }
    }
}
