// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK

using System;

namespace Microsoft.Build.Tasks
{
    public abstract class TaskRequiresFramework : TaskExtension
    {
        internal TaskRequiresFramework(string taskName) => TaskName = taskName;

        private string TaskName { get; set; }

        /// <summary>
        /// Task entry point.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogErrorWithCodeFromResources("TaskRequiresFrameworkFailure", TaskName);
            return false;
        }
    }
}

#endif
