// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
