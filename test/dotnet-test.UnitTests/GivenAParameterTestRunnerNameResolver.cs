// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenAParameterTestRunnerNameResolver
    {
        private const string SomeTestRunner = "Some test runner";

        [Fact]
        public void It_returns_the_runner_based_on_the_parameter()
        {
            var parameterTestRunnerResolver = new ParameterTestRunnerNameResolver(SomeTestRunner);

            var testRunner = parameterTestRunnerResolver.ResolveTestRunner();

            testRunner.Should().Be($"dotnet-test-{SomeTestRunner}");
        }
    }
}
