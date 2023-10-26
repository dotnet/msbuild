// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.FileAccess;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     Only one plugin instance can exist for a given BuildManager BeginBuild / EndBuild session.
    ///     Any exceptions thrown by the plugin will cause MSBuild to fail the build.
    /// </summary>
    public abstract class ProjectCachePluginBase
    {
        /// <summary>
        ///     Called once before the build, to have the plugin instantiate its state.
        ///     Errors are checked via <see cref="PluginLoggerBase.HasLoggedErrors" />.
        /// </summary>
        public abstract Task BeginBuildAsync(
            CacheContext context,
            PluginLoggerBase logger,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Called once for each build request.
        ///     Operation needs to be atomic. Any side effects (IO, environment variables, etc) need to be reverted upon
        ///     cancellation.
        ///     MSBuild may choose to cancel this method and build the project itself.
        ///     Errors are checked via <see cref="PluginLoggerBase.HasLoggedErrors" />.
        /// </summary>
        public abstract Task<CacheResult> GetCacheResultAsync(
            BuildRequestData buildRequest,
            PluginLoggerBase logger,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Called once after all the build to let the plugin do any post build operations (log metrics, cleanup, etc).
        ///     Errors are checked via <see cref="PluginLoggerBase.HasLoggedErrors" />.
        /// </summary>
        public abstract Task EndBuildAsync(PluginLoggerBase logger, CancellationToken cancellationToken);

        /// <summary>
        ///     Called for each file access from an MSBuild node or one of its children.
        /// </summary>
        [CLSCompliant(false)]
        public virtual void HandleFileAccess(FileAccessContext fileAccessContext, FileAccessData fileAccessData)
        {
        }

        /// <summary>
        ///     Called for each new child process created by an MSBuild node or one of its children.
        /// </summary>
        [CLSCompliant(false)]
        public virtual void HandleProcess(FileAccessContext fileAccessContext, ProcessData processData)
        {
        }

        /// <summary>
        ///     Called when a build request finishes execution. This provides an opportunity for the plugin to take action on the
        ///     aggregated file access reports from <see cref="HandleFileAccess(FileAccessContext, FileAccessData)"/>.
        ///     Errors are checked via <see cref="PluginLoggerBase.HasLoggedErrors" />.
        /// </summary>
        public virtual Task HandleProjectFinishedAsync(
            FileAccessContext fileAccessContext,
            BuildResult buildResult,
            PluginLoggerBase logger,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
