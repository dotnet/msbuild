// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewCSApp : TestBase
    {
        [Fact]
        public void When_dotnet_build_is_invoked_Then_app_builds_without_warnings()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore /p:SkipInvalidConfigurations=true");

            var buildResult = new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .ExecuteWithCapturedOutput("build");
            
            buildResult.Should().Pass()
                       .And.NotHaveStdErr();
        }

        [Fact]
        public void When_dotnet_new_is_invoked_mupliple_times_it_should_fail()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            var result = new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .ExecuteWithCapturedOutput("new");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.Equal(expectedState, actualState);

            result.Should().Fail()
                  .And.HaveStdErr();
        }
        
        internal static void AddProjectDependency(string projectFilePath, string dependencyId, string dependencyVersion)
        {
            var projectRootElement = ProjectRootElement.Open(projectFilePath);

            projectRootElement.AddItem("PackageReference", dependencyId, new Dictionary<string, string>{{"Version", dependencyVersion}});

            projectRootElement.Save();
        }
    }
}
