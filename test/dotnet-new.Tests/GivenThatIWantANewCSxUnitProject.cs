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

namespace Microsoft.DotNet.Tests
{
    public class GivenThatIWantANewCSxUnitProject : TestBase
    {
        
        [Fact]
        public void When_xUnit_project_created_Then_project_restores()
        {
            var rootPath = Temp.CreateDirectory().Path;
            var projectJsonFile = Path.Combine(rootPath, "project.json");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new --type xunittest")
                .Should()
                .Pass();
            
            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore")
                .Should().Pass();
            
        }

        [Fact]
        public void When_dotnet_test_is_invoked_Then_tests_run_without_errors()
        {
            const string testFolder = "test";
            var rootPath = Temp.CreateDirectory().Path;
            var testDirectory = Directory.CreateDirectory(Path.Combine(rootPath, testFolder)).FullName;

            File.WriteAllText(Path.Combine(rootPath, "global.json"), $"{{ \"projects\": [\"{testFolder}\"] }}");

            new TestCommand("dotnet") { WorkingDirectory = testDirectory }
                .Execute("new --type xunittest");

            new TestCommand("dotnet") { WorkingDirectory = testDirectory }
                .Execute("restore");

            var buildResult = new TestCommand("dotnet")
                .WithWorkingDirectory(testDirectory)
                .ExecuteWithCapturedOutput("test")
                .Should()
                .Pass()
                .And
                .NotHaveStdErr();
        }


    }
}
