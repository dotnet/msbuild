// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenAProjectJsonTestRunnerNameResolver
    {
        private const string SomeTestRunner = "runner";

        [Fact]
        public void It_resolves_the_TestRunner_using_the_testRunner_property_in_the_projectJson()
        {
            var project = new Project
            {
                TestRunner = SomeTestRunner
            };

            var projectJsonTestRunnerResolver = new ProjectJsonTestRunnerNameResolver(project);

            var testRunner = projectJsonTestRunnerResolver.ResolveTestRunner();

            testRunner.Should().Be($"dotnet-test-{SomeTestRunner}");
        }

        [Fact]
        public void It_returns_null_when_there_is_no_testRunner_set_in_the_projectJson()
        {
            var project = new Project();

            var projectJsonTestRunnerResolver = new ProjectJsonTestRunnerNameResolver(project);

            var testRunner = projectJsonTestRunnerResolver.ResolveTestRunner();

            testRunner.Should().BeNull();
        }
    }
}
