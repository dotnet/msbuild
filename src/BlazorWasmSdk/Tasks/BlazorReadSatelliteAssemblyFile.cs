// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class BlazorReadSatelliteAssemblyFile : Task
    {
        [Output]
        public ITaskItem[] SatelliteAssembly { get; set; }

        [Required]
        public ITaskItem ReadFile { get; set; }

        public override bool Execute()
        {
            var document = XDocument.Load(ReadFile.ItemSpec);
            SatelliteAssembly = document.Root
                .Elements()
                .Select(e =>
                {
                    // <Assembly Name="..." Culture="..." DestinationSubDirectory="..." />

                    var taskItem = new TaskItem(e.Attribute("Name").Value);
                    taskItem.SetMetadata("Culture", e.Attribute("Culture").Value);
                    taskItem.SetMetadata("DestinationSubDirectory", e.Attribute("DestinationSubDirectory").Value);

                    return taskItem;
                }).ToArray();

            return true;
        }
    }
}
