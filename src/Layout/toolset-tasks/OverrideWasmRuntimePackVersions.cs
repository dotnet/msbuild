// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// </summary>
    public sealed class OverrideWasmRuntimePackVersions : Task
    {
        [Required]
        public ITaskItem[] Properties { get; set; }

        [Required]
        public string WorkloadManifestPath { get; set; }

        public override bool Execute()
        {
            var document = XDocument.Load(WorkloadManifestPath);
            if (document != null)
            {
                foreach (var property in Properties)
                {
                    string propertyName = property.ItemSpec;
                    string targetVersion = property.GetMetadata("Version");

                    var xmlProperty = document.Descendants(propertyName).Single();
                    xmlProperty.Value = targetVersion;
                }

                document.Save(WorkloadManifestPath);

                return true;
            }
            
            return false;
        }
    }
}
