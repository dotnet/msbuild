// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// The result code for a given target.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32", Justification = "TargetResultCode is serialized - additional bytes waste bandwidth")]
    public enum TargetResultCode : byte
    {
        /// <summary>
        /// The target was skipped because its condition was not met.
        /// </summary>
        Skipped,

        /// <summary>
        /// The target successfully built.
        /// </summary>
        Success,

        /// <summary>
        /// The target failed to build.
        /// </summary>
        Failure,
    }

    /// <summary>
    /// An interface representing results for a specific target
    /// </summary>
    public interface ITargetResult
    {
        /// <summary>
        /// The exception generated when the target ran, if any.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// The set of build items output by the target.
        /// These are ITaskItem's, so they have no item-type.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1819:PropertiesShouldNotReturnArrays",
            Justification =
                "This isn't worth fixing. The current code depends too on the fact that TaskItem[] can implicitly cast to ITaskItem[] but the same is not true for List<TaskItem> and List<ITaskItem>. Also a public interface (IBuildEngine) would have to be changed, or the items copied into an array")]
        ITaskItem[] Items { get; }

        /// <summary>
        /// The result code for the target run.
        /// </summary>
        TargetResultCode ResultCode { get; }
    }
}
