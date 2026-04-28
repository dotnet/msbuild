// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that calls IBuildEngine9.RequestCores with the same fallback pattern
    /// used by real callers (MonoAOTCompiler, EmccCompile, ILStrip, EmitBundleBase):
    ///   try { cores = be9.RequestCores(N); }
    ///   catch (NotImplementedException) { be9 = null; /* use own estimate */ }
    ///   finally { be9?.ReleaseCores(cores); }
    /// Used by regression tests for https://github.com/dotnet/msbuild/issues/13333
    /// </summary>
    public class RequestCoresWithFallbackTask : Task
    {
        /// <summary>
        /// Number of cores to request. Defaults to 1.
        /// </summary>
        public int CoreCount { get; set; } = 1;

        /// <summary>
        /// Whether to call ReleaseCores after the parallel work (via be9?.ReleaseCores pattern).
        /// </summary>
        public bool ReleaseAfter { get; set; }

        /// <summary>
        /// Number of cores granted (either from RequestCores or the fallback value).
        /// </summary>
        [Output]
        public int GrantedCores { get; set; }

        /// <summary>
        /// True if NotImplementedException was caught and the fallback was used.
        /// </summary>
        [Output]
        public bool UsedFallback { get; set; }

        public override bool Execute()
        {
            int allowedParallelism = CoreCount;
            IBuildEngine9? be9 = BuildEngine as IBuildEngine9;
            try
            {
                if (be9 is not null)
                {
                    allowedParallelism = be9.RequestCores(CoreCount);
                }
            }
            catch (NotImplementedException)
            {
                // This is the expected path when callbacks are not supported.
                // Real callers null out be9 so ReleaseCores is skipped.
                Log.LogMessage(MessageImportance.High, "RequestCores threw NotImplementedException, using fallback");
                be9 = null;
                UsedFallback = true;
            }

            GrantedCores = allowedParallelism;
            Log.LogMessage(MessageImportance.High, $"GrantedCores = {GrantedCores}");

            // Simulate parallel work...

            if (ReleaseAfter)
            {
                // Real callers use be9?.ReleaseCores — skipped when be9 was nulled in catch.
                if (be9 is not null)
                {
                    be9.ReleaseCores(allowedParallelism);
                    Log.LogMessage(MessageImportance.High, $"ReleaseCores({allowedParallelism}) completed");
                }
            }

            return true;
        }
    }
}
