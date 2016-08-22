// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test
{
    public static class TestMessageTypes
    {
        public const string TestRunnerExecute = "TestRunner.Execute";
        public const string TestRunnerWaitingCommand = "TestRunner.WaitingCommand";
        public const string TestRunnerTestResult = "TestExecution.TestResult";
        public const string TestRunnerTestStarted = "TestExecution.TestStarted";
        public const string TestRunnerTestCompleted = "TestRunner.TestCompleted";
        public const string TestRunnerTestFound = "TestDiscovery.TestFound";
        public const string TestSessionConnected = "TestSession.Connected";
        public const string TestSessionTerminate = "TestSession.Terminate";
        public const string VersionCheck = "ProtocolVersion";
        public const string TestDiscoveryStart = "TestDiscovery.Start";
        public const string TestDiscoveryCompleted = "TestDiscovery.Completed";
        public const string TestDiscoveryTestFound = "TestDiscovery.TestFound";
        public const string TestExecutionGetTestRunnerProcessStartInfo = "TestExecution.GetTestRunnerProcessStartInfo";
        public const string TestExecutionTestRunnerProcessStartInfo = "TestExecution.TestRunnerProcessStartInfo";
        public const string TestExecutionStarted = "TestExecution.TestStarted";
        public const string TestExecutionTestResult = "TestExecution.TestResult";
        public const string TestExecutionCompleted = "TestExecution.Completed";
    }
}
