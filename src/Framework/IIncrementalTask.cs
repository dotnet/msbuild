// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks which is supports incrementality.
    /// </summary>
    /// <remarks>The tasks implementing this interface should return false to stop the build when in <see cref="FailIfNotIncremental"/> is true and task is not fully incremental.  Try to provide helpful information to diagnose incremental behavior.</remarks>
    public interface IIncrementalTask
    {
        /// <summary>
        /// Set by MSBuild when Question flag is used.
        /// </summary>
        bool FailIfNotIncremental { set; }
    }
}
