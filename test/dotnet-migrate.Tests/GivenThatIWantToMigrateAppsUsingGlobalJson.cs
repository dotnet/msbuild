// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateAppsUsingGlobalJson : TestBase
    {
        [Fact]
        public void ItMigratesWhenBeingPassedAFullPathToGlobalJson()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("AppWithPackageNamedAfterFolder").Path;
            var globalJsonPath = Path.Combine(solutionDirectory, "global.json");

            new TestCommand("dotnet")
                    .WithForwardingToConsole()
                    .Execute($"migrate {globalJsonPath}")
                    .Should()
                    .Pass();
        }

        [Fact]
        public void ItMigratesWhenBeingPassedJustGlobalJson()
        {
            var solutionDirectory =
                TestAssetsManager.CreateTestInstance("AppWithPackageNamedAfterFolder").Path;

            new TestCommand("dotnet")
                    .WithWorkingDirectory(solutionDirectory)
                    .WithForwardingToConsole()
                    .Execute($"migrate global.json")
                    .Should()
                    .Pass();
        }
    }
}