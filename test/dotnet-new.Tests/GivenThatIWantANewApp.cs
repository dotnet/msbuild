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
    public class GivenThatIWantANewApp : TestBase
    {
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
 
        [Fact] 
        public void RestoreDoesNotUseAnyCliProducedPackagesOnItsTemplates() 
        { 
            var cSharpTemplates = new [] { "Console", "Lib", "Web", "Mstest", "XUnittest" }; 
 
            var rootPath = TestAssetsManager.CreateTestDirectory().Path; 
            var packagesDirectory = Path.Combine(rootPath, "packages"); 
 
            foreach (var cSharpTemplate in cSharpTemplates) 
            { 
                var projectFolder = Path.Combine(rootPath, cSharpTemplate); 
                Directory.CreateDirectory(projectFolder); 
                CreateAndRestoreNewProject(cSharpTemplate, projectFolder, packagesDirectory); 
            } 
 
            Directory.EnumerateFiles(packagesDirectory, $"*.nupkg", SearchOption.AllDirectories) 
                .Should().NotContain(p => p.Contains("Microsoft.DotNet.Cli.Utils")); 
        } 
 
        private void CreateAndRestoreNewProject( 
            string projectType, 
            string projectFolder, 
            string packagesDirectory) 
        { 
            new TestCommand("dotnet") { WorkingDirectory = projectFolder } 
                .Execute($"new --type {projectType}") 
                .Should().Pass(); 
 
            new RestoreCommand() 
                .WithWorkingDirectory(projectFolder) 
                .Execute($"--packages {packagesDirectory} /p:SkipInvalidConfigurations=true") 
                .Should().Pass(); 
        } 
    }
}
