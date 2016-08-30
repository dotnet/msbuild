// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test
{
    public enum DotnetTestState
    {
        NoOp,
        InitialState,
        VersionCheckCompleted,
        TestDiscoveryStarted,
        TestDiscoveryCompleted,
        TestExecutionSentTestRunnerProcessStartInfo,
        TestExecutionStarted,
        TestExecutionCompleted,
        Terminated
    }
}
