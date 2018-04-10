// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASolutionWithProjRefDiffCase : SdkTest
    {
        public GivenThatWeWantToBuildASolutionWithProjRefDiffCase(ITestOutputHelper log) : base(log)
        {
        }

        [PlatformSpecificFact(Platform.Windows, Platform.Darwin)]
        public void ItBuildsTheSolutionSuccessfully()
        {
            const string solutionFile = "AppWithProjRefCaseDiff.sln";

            var asset = _testAssetsManager
                .CopyTestAsset("AppWithProjRefCaseDiff")
                .WithSource()
                .Restore(Log, solutionFile);

            var command = new BuildCommand(Log, Path.Combine(asset.TestRoot, solutionFile));
            command.Execute().Should().Pass();
        }
    }
}
