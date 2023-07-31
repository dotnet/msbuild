// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class FindAssembliesWithReferencesTo : Task
    {
        [Required]
        public ITaskItem[] TargetAssemblyNames { get; set; }

        [Required]
        public ITaskItem[] Assemblies { get; set; }

        [Output]
        public string[] ResolvedAssemblies { get; set; }

        public override bool Execute()
        {
            var referenceItems = new List<AssemblyItem>(Assemblies.Length);
            foreach (var item in Assemblies)
            {
                const string FusionNameKey = "FusionName";
                var fusionName = item.GetMetadata(FusionNameKey);
                if (string.IsNullOrEmpty(fusionName))
                {
                    Log.LogError($"Missing required metadata '{FusionNameKey}' for '{item.ItemSpec}.");
                    return false;
                }

                var assemblyName = new AssemblyName(fusionName).Name;
                referenceItems.Add(new AssemblyItem
                {
                    AssemblyName = assemblyName,
                    IsFrameworkReference = item.GetMetadata("IsFrameworkReference") == "true",
                    Path = item.ItemSpec,
                });
            }

            var targetAssemblyNames = TargetAssemblyNames.Select(s => s.ItemSpec).ToList();

            var provider = new ReferenceResolver((IReadOnlyList<string>)targetAssemblyNames, referenceItems);
            try
            {
                var assemblyNames = provider.ResolveAssemblies();
                ResolvedAssemblies = assemblyNames.ToArray();
            }
            catch (ReferenceAssemblyNotFoundException ex)
            {
                // Print a warning and return. We cannot produce a correct document at this point.
                var warning = "Reference assembly {0} could not be found. This is typically caused by build errors in referenced projects.";
                Log.LogWarning(null, "RAZORSDK1007", null, null, 0, 0, 0, 0, warning, ex.FileName);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
