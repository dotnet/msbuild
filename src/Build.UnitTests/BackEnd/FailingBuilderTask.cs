// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    public  class FailingBuilderTask : Task
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
