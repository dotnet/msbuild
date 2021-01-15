// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Framework;

namespace MockCacheFromAssembly
{
    public class AssemblyMockCache : ProjectCachePluginBase
    {
        public AssemblyMockCache()
        {
            ThrowFrom("Constructor");
        }

        public override Task BeginBuildAsync(CacheContext context, PluginLoggerBase logger, CancellationToken cancellationToken)
        {
            logger.LogMessage($"{nameof(AssemblyMockCache)}: BeginBuildAsync", MessageImportance.High);

            ThrowFrom(nameof(BeginBuildAsync));

            return Task.CompletedTask;
        }

        public override Task<CacheResult> GetCacheResultAsync(
            BuildRequestData buildRequest,
            PluginLoggerBase logger,
            CancellationToken cancellationToken)
        {
            logger.LogMessage($"{nameof(AssemblyMockCache)}: GetCacheResultAsync for {buildRequest.ProjectFullPath}", MessageImportance.High);

            ThrowFrom(nameof(GetCacheResultAsync));

            return Task.FromResult(CacheResult.IndicateNonCacheHit(CacheResultType.CacheNotApplicable));
        }

        public override Task EndBuildAsync(PluginLoggerBase logger, CancellationToken cancellationToken)
        {
            logger.LogMessage($"{nameof(AssemblyMockCache)}: EndBuildAsync", MessageImportance.High);

            ThrowFrom(nameof(EndBuildAsync));

            return Task.CompletedTask;
        }

        private static void ThrowFrom(string throwFrom)
        {
            if (Environment.GetEnvironmentVariable(throwFrom) != null)
            {
                throw new Exception($"Cache plugin exception from {throwFrom}");
            }
        }
    }
}
