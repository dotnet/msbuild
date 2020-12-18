// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     A cache hit can use this to instruct MSBuild to construct a BuildResult with the target result specified in this
    ///     type.
    /// </summary>
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
