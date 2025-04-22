// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.ProjectCache
{
#pragma warning disable CS0618  // suppress “obsolete” warnings in this file due to this referencing other Experimental.ProjectCache types
    /// <summary>
    ///     A cache hit can use this to instruct MSBuild to construct a BuildResult with the target result specified in this
    ///     type.
    /// </summary>
    [Obsolete("This class is moved to Microsoft.Build.ProjectCache namespace.", false)]
    public readonly struct PluginTargetResult
    {
        public string TargetName { get; }
        public IReadOnlyCollection<ITaskItem2> TaskItems { get; }
        public BuildResultCode ResultCode { get; }

        public PluginTargetResult(
            string targetName,
            IReadOnlyCollection<ITaskItem2> taskItems,
            BuildResultCode resultCode)
        {
            TargetName = targetName;
            TaskItems = taskItems;
            ResultCode = resultCode;
        }
    }
}
