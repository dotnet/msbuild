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
    public class GivenThatIWantANewCSLibrary : TestBase
    {
        [Fact]
        public void When_library_created_Then_project_restores()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new --type lib")
                .Should().Pass();
            
            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore /p:SkipInvalidConfigurations=true")
                .Should().Pass();
            
        }

        [Fact]
        public void When_dotnet_build_is_invoked_Then_library_builds_without_warnings()
        {
            var rootPath = TestAssetsManager.CreateTestDirectory().Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("new --type lib");

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute("restore /p:SkipInvalidConfigurations=true");

            var buildResult = new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("build")
                .Should().Pass()
                .And.NotHaveStdErr();
        }
    }
}
