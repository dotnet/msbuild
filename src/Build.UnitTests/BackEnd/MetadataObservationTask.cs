// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Reports the value of a metadata name as the task itself observes it, plus the id of the process the
    /// task ran in, so tests can tell an in-proc execution apart from a task host one.
    /// Optionally reassigns ItemSpec first, to exercise metadata that derives from it.
    /// </summary>
    public class MetadataObservationTask : Task
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string MetadataName { get; set; }

        public string NewItemSpec { get; set; }

        [Output]
        public string ObservedValue { get; set; }

        [Output]
        public int TaskProcessId { get; set; }

        public override bool Execute()
        {
            TaskProcessId = Process.GetCurrentProcess().Id;

            if (Items.Length > 0)
            {
                ITaskItem item = Items[0];

                if (!string.IsNullOrEmpty(NewItemSpec))
                {
                    item.ItemSpec = NewItemSpec;
                }

                ObservedValue = item.GetMetadata(MetadataName);
            }
            else
            {
                ObservedValue = string.Empty;
            }

            return true;
        }
    }
}

