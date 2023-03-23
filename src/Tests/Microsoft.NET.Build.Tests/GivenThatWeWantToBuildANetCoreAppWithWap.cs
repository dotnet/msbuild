// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppWithWap : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppWithWap(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void WhenNetCoreProjectIsReferencedByAWapProject()
        {
            var testInstance = _testAssetsManager
                .CopyTestAsset("TestAppWithWapAndWpf")
                .WithSource();

            new BuildCommand(testInstance, "WapProjTemplate1")
                .Execute()
                .Should()
                .Pass();
        }
    }
}
