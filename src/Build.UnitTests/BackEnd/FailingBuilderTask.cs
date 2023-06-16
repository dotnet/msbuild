// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class FailingBuilderTask : Task
    {
        public FailingBuilderTask()
            : base(null)
        { }

        public override bool Execute()
        {
            // BuildEngine.BuildProjectFile is how the GenerateTemporaryTargetAssembly task builds projects.
            return BuildEngine.BuildProjectFile(CurrentProject, new string[] { "ErrorTask" }, new Hashtable(), null);
        }

        [Required]
        public string CurrentProject { get; set; }
    }
}
