// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Testing.Abstractions
{
    public interface ITestExecutionSink : ITestSink
    {
        void SendTestStarted(Test test);

        void SendTestResult(TestResult testResult);
    }
}
