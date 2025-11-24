// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Internal interface that provides build host context information to task factories during initialization.
    /// This allows task factories to query the build environment and make appropriate compilation decisions.
    /// </summary>
    /// <remarks>
    /// This interface is internal and not intended for use by external task factory implementations.
    /// It is specifically designed for MSBuild's built-in inline task factories (RoslynCodeTaskFactory,
    /// CodeTaskFactory, XamlTaskFactory) to determine whether they should compile for out-of-process execution
    /// based on the build host's multi-threaded configuration.
    /// </remarks>
    internal interface ITaskFactoryBuildParameterProvider
    {
        /// <summary>
        /// Gets a value indicating whether the build is running in multi-threaded mode (/mt flag).
        /// </summary>
        /// <remarks>
        /// This property allows task factories to determine if they should compile for out-of-process execution
        /// during their Initialize() method. When true, inline task factories should compile to disk instead of
        /// in-memory to enable out-of-process execution.
        /// </remarks>
        bool IsMultiThreadedBuild { get; }

        /// <summary>
        /// Gets a value indicating whether task factories should be forced to execute out-of-process
        /// regardless of the multi-threaded build setting.
        /// </summary>
        /// <remarks>
        /// This property exposes the value of the MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC environment variable
        /// through the host context, allowing task factories to avoid direct dependency on the Traits class.
        /// This improves testability by enabling behavior injection through the interface.
        /// </remarks>
        bool ForceOutOfProcessExecution { get; }
    }
}
