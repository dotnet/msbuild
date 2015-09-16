// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
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
            Assembly coreAssembly = typeof(object).GetTypeInfo().Assembly;

            var coreAssemblyFileVersion = coreAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (coreAssemblyFileVersion == null)
            {
                Log.LogError("No AssemblyFileVersionAttribute found on core assembly");
            }
            else
            {
                Log.LogMessage($"Core assembly file version: {coreAssemblyFileVersion.Version}");
            }

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
