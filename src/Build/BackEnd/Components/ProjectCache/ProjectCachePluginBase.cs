// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     Only one plugin instance can exist for a given BuildManager BeginBuild / EndBuild session.
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
    }
}
