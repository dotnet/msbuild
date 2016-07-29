// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatIWantANewCSnUnitProject : TestBase
    {
        
        [Fact]
        public void When_nUnit_project_created_Then_project_restores()
        {
            var rootPath = Temp.CreateDirectory().Path;
            var projectJsonFile = Path.Combine(rootPath, "project.json");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new --type nunittest")
                .Should()
                .Pass();
            
            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore")
                .Should().Pass();
            
        }

        [Fact]
        public void When_dotnet_test_is_invoked_Then_tests_run_without_errors()
        {
            var rootPath = Temp.CreateDirectory().Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new --type nunittest");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore");

            var buildResult = new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("test")
                .Should()
                .Pass()
                .And
                .NotHaveStdErr();
        }
    }
}
