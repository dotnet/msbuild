// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;

#nullable disable

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
