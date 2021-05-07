// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppWithWap : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppWithWap(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresSpecificFrameworkFact("net5.0")]
        public void WhenNetCoreProjectIsReferencedByAWapProject()
        {
            var testInstance = _testAssetsManager
                .CopyTestAsset("TestAppWithWapAndWpf")
                .WithSource();

            // var projectDirectory = testInstance.Path;
            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();
        }
    }
}
