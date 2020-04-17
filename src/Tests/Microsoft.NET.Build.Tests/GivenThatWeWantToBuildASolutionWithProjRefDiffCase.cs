// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASolutionWithProjRefDiffCase : SdkTest
    {
        public GivenThatWeWantToBuildASolutionWithProjRefDiffCase(ITestOutputHelper log) : base(log)
        {
        }

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX)]
        public void ItBuildsTheSolutionSuccessfully()
        {
            const string solutionFile = "AppWithProjRefCaseDiff.sln";

            var asset = _testAssetsManager
                .CopyTestAsset("AppWithProjRefCaseDiff")
                .WithSource();

            var command = new BuildCommand(Log, Path.Combine(asset.TestRoot, solutionFile));
            command.Execute().Should().Pass();
        }
    }
}
