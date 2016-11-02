// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewCSAppWithSpecifiedType : TestBase
    {
        [Theory]
        [InlineData("Console", false)]
        [InlineData("Lib", false)]
        [InlineData("Web", true)]
        [InlineData("Mstest", false)]
        [InlineData("XUnittest", false)]
        public void When_dotnet_build_is_invoked_then_project_restores_and_builds_without_warnings(
            string projectType,
            bool useNuGetConfigForAspNet)
        {
            var rootPath = TestAssetsManager.CreateTestDirectory(callingMethod: "i").Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"new --type {projectType}")
                .Should().Pass();

            if (useNuGetConfigForAspNet)
            {
                File.Copy("NuGet.tempaspnetpatch.config", Path.Combine(rootPath, "NuGet.Config"));
            }

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"restore /p:SkipInvalidConfigurations=true")
                .Should().Pass();

            var buildResult = new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("build")
                .Should().Pass()
                .And.NotHaveStdErr();
        }
    }
}
