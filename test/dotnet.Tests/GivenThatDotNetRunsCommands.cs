// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatDotNetRunsCommands : TestBase
    {
        [Fact]
        public void UnresolvedPlatformReferencesFailAsExpected()
        {
            var testAssetsManager = GetTestGroupTestAssetsManager("NonRestoredTestProjects");
            var testInstance = testAssetsManager.CreateTestInstance("TestProjectWithUnresolvedPlatformDependency");

            new RestoreCommand()
                .WithWorkingDirectory(testInstance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Fail();
            new DirectoryInfo(testInstance.TestRoot)
                .Should()
                .HaveFile("project.lock.json");

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.TestRoot)
                .ExecuteWithCapturedOutput("crash")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("No executable found matching command \"dotnet-crash\"");
        }
    }
}
