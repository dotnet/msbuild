// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test
{
    public class ParameterTestRunnerNameResolver : ITestRunnerNameResolver
    {
        private readonly string _testRunner;

        public ParameterTestRunnerNameResolver(string testRunner)
        {
            _testRunner = testRunner;
        }

        public string ResolveTestRunner()
        {
            return $"dotnet-test-{_testRunner}";
        }
    }
}
