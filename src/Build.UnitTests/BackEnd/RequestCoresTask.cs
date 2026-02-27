// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A test task that calls IBuildEngine9.RequestCores and optionally ReleaseCores.
    /// Used by TaskHostCallback_Tests (in-process) and NetTaskHost_E2E_Tests (cross-runtime).
    /// The E2E project includes this file via linked compile to avoid duplication.
    /// </summary>
    public class RequestCoresTask : Task
    {
        /// <summary>
        /// Number of cores to request. Defaults to 1.
        /// </summary>
        public int CoreCount { get; set; } = 1;

        /// <summary>
        /// Whether to release the granted cores after requesting them.
        /// </summary>
        public bool ReleaseAfter { get; set; }

        /// <summary>
        /// Number of cores granted by the build engine.
        /// </summary>
        [Output]
        public int GrantedCores { get; set; }

        public override bool Execute()
        {
            if (BuildEngine is IBuildEngine9 engine9)
            {
                GrantedCores = engine9.RequestCores(CoreCount);
                Log.LogMessage(MessageImportance.High, $"RequestCores({CoreCount}) = {GrantedCores}");

                if (ReleaseAfter && GrantedCores > 0)
                {
                    engine9.ReleaseCores(GrantedCores);
                    Log.LogMessage(MessageImportance.High, $"ReleaseCores({GrantedCores}) completed");
                }

                return true;
            }

            Log.LogError("BuildEngine does not implement IBuildEngine9");
            return false;
        }
    }
}
