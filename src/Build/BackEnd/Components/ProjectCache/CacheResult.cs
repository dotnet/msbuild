// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    /// Result types that a plugin can return for a given build request.
    /// </summary>
    public enum CacheResultType
    {
        /// <summary>
        /// The plugin failed and couldn't return a result. The plugin should log an error detailing the failure. MSBuild will stop the build.
        /// </summary>
        None = 0,

        /// <summary>
        /// The plugin determined that it supports a build request and found that it can be skipped. MSBuild won't build the request.
        /// </summary>
        CacheHit,

        /// <summary>
        /// The plugin determined that it supports a build request and found that it cannot be skipped. MSBuild will build the request.
        /// </summary>
        CacheMiss,

        /// <summary>
        /// The plugin determined that it does not support a certain build request. MSBuild will build the request.
        /// For example, a plugin may not support projects with a certain extension, certain properties, or certain called targets.
        /// </summary>
        CacheNotApplicable
    }

    /// <summary>
    ///     Represents the cache result a plugin returns back to MSBuild when queried about a certain project.
    ///     Results representing cache hits (with <see cref="ResultType"/> == <see cref="CacheResultType.CacheHit"/>)
    ///     contain information about what <see cref="Execution.BuildResult"/> MSBuild should use for the queried project.
    ///     It is assumed that all cache hits result in a successful <see cref="Execution.BuildResult"/>.
    /// </summary>
    public class CacheResult
    {
        private CacheResult(
            CacheResultType resultType,
            BuildResult? buildResult = null,
            ProxyTargets? proxyTargets = null)
        {
            if (resultType == CacheResultType.CacheHit)
            {
                ErrorUtilities.VerifyThrow(
                    buildResult != null ^ proxyTargets != null,
                    "Either buildResult is specified, or proxyTargets is specified. Not both.");
            }

            ResultType = resultType;
            BuildResult = buildResult;
            ProxyTargets = proxyTargets;
        }

        public CacheResultType ResultType { get; }
        public BuildResult? BuildResult { get; }
        public ProxyTargets? ProxyTargets { get; }

        public static CacheResult IndicateCacheHit(BuildResult buildResult)
        {
            return new CacheResult(CacheResultType.CacheHit, buildResult);
        }

        public static CacheResult IndicateCacheHit(ProxyTargets proxyTargets)
        {
            return new CacheResult(CacheResultType.CacheHit, proxyTargets: proxyTargets);
        }

        public static CacheResult IndicateCacheHit(IReadOnlyCollection<PluginTargetResult> targetResults)
        {
            ErrorUtilities.VerifyThrowArgumentLength(targetResults, nameof(targetResults));

            return new CacheResult(CacheResultType.CacheHit, ConstructBuildResult(targetResults));
        }

        public static CacheResult IndicateNonCacheHit(CacheResultType resultType)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(resultType != CacheResultType.CacheHit, "CantBeCacheHit");
            return new CacheResult(resultType);
        }

        private static BuildResult ConstructBuildResult(IReadOnlyCollection<PluginTargetResult> targetResults)
        {
            var buildResult = new BuildResult();

            foreach (var pluginTargetResult in targetResults)
            {
                buildResult.AddResultsForTarget(
                    pluginTargetResult.TargetName,
                    new TargetResult(
                        pluginTargetResult.TaskItems.Select(ti => CreateTaskItem(ti)).ToArray(),
                        CreateWorkUnitResult(pluginTargetResult.ResultCode)));
            }

            return buildResult;
        }

        private static WorkUnitResult CreateWorkUnitResult(BuildResultCode resultCode)
        {
            return resultCode == BuildResultCode.Success
                ? new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null)
                : new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Stop, null);
        }

        private static ProjectItemInstance.TaskItem CreateTaskItem(ITaskItem2 taskItemInterface)
        {
            var taskItem = new ProjectItemInstance.TaskItem(taskItemInterface.EvaluatedIncludeEscaped, null);

            foreach (string metadataName in taskItemInterface.MetadataNames)
            {
                taskItem.SetMetadata(metadataName, taskItemInterface.GetMetadataValueEscaped(metadataName));
            }

            return taskItem;
        }
    }
}
