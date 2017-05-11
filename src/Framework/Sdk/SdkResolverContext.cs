// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Context used by an <see cref="SdkResolver" /> to resolve an SDK.
    /// </summary>
    public abstract class SdkResolverContext
    {
        /// <summary>
        ///     Logger to log real-time messages back to MSBuild.
        /// </summary>
        public virtual SdkLogger Logger { get; protected set; }

        /// <summary>
        ///     Path to the project file being built.
        /// </summary>
        public virtual string ProjectFilePath { get; protected set; }

        /// <summary>
        ///     Path to the solution file being built, if known. May be null.
        /// </summary>
        public virtual string SolutionFilePath { get; protected set; }
    }
}
