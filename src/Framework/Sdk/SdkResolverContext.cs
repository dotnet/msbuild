// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     Context used by an <see cref="SdkResolver" /> to resolve an SDK.
    /// </summary>
    public abstract class SdkResolverContext
    {
        /// <summary>
        /// Gets a value indicating if the resolver is allowed to be interactive.
        /// </summary>
        public virtual bool Interactive { get; protected set; }

        /// <summary>
        /// Gets a value indicating if the resolver is running in Visual Studio.
        /// </summary>
        public virtual bool IsRunningInVisualStudio { get; protected set; }

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

        /// <summary>
        ///     Version of MSBuild currently running.
        /// </summary>
        /// <remarks>
        ///    <format type="text/markdown"><![CDATA[
        /// ## Remarks
        ///
        /// File version is informational and not equal to the assembly version.
        /// ]]></format>
        /// </remarks>
        public virtual Version MSBuildVersion { get; protected set; }

        /// <summary>
        ///     Gets or sets any custom state for current build.  This allows resolvers to maintain state between resolutions.
        ///     This property is not thread-safe.
        /// </summary>
        public virtual object State { get; set; }
    }
}
