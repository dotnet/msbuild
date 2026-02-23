// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// A simple task that queries IsRunningMultipleNodes from the build engine.
    /// Used by TaskHostCallback_Tests for in-process integration tests.
    /// See also CallbackTestTask in ExampleNetTask for cross-runtime E2E tests.
    /// </summary>
    public class IsRunningMultipleNodesTask : Task
    {
        [Output]
        public bool IsRunningMultipleNodes { get; set; }

        public override bool Execute()
        {
            if (BuildEngine is IBuildEngine2 engine2)
            {
                IsRunningMultipleNodes = engine2.IsRunningMultipleNodes;
                Log.LogMessage(MessageImportance.High, $"IsRunningMultipleNodes = {IsRunningMultipleNodes}");
                return true;
            }

            Log.LogError("BuildEngine does not implement IBuildEngine2");
            return false;
        }
    }
}
