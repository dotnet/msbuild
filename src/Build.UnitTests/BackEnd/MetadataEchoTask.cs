// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Returns the value of a metadata name as seen by the task itself, so tests can observe what a task
    /// actually receives rather than what the engine would expand on its behalf.
    /// </summary>
    public class MetadataEchoTask : Task
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string MetadataName { get; set; }

        [Output]
        public string MetadataValue { get; set; }

        [Output]
        public int Pid { get; set; }

        public override bool Execute()
        {
            MetadataValue = Items.Length > 0 ? Items[0].GetMetadata(MetadataName) : string.Empty;
            Pid = Process.GetCurrentProcess().Id;
            return true;
        }
    }
}
