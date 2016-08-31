// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestRunnerOperationFailedException : Exception
    {
        public string TestRunner { get; set; }
        public int ExitCode { get; set; }
        public override string Message => $"'{TestRunner}' returned '{ExitCode}'.";

        public TestRunnerOperationFailedException(string testRunner, int exitCode)
        {
            TestRunner = testRunner;
            ExitCode = exitCode;
        }
    }
}
