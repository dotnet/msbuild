// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// The reason that a target was built by its parent target.
    /// </summary>
    public enum TargetBuiltReason
    {
        /// <summary>
        /// This wasn't built on because of a parent.
        /// </summary>
        None,

        /// <summary>
        /// The target was part of the parent's BeforeTargets list.
        /// </summary>
        BeforeTargets,

        /// <summary>
        /// The target was part of the parent's DependsOn list.
        /// </summary>
        DependsOn,

        /// <summary>
        /// The target was part of the parent's AfterTargets list.
        /// </summary>
        AfterTargets
    }
}
