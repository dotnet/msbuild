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
    public class GivenThatIWantANewAppWithSpecifiedType : TestBase
    {
        [Theory]
        [InlineData("C#", "Console", false)]
        [InlineData("C#", "Lib", false)]
        [InlineData("C#", "Web", true)]
        [InlineData("C#", "Mstest", false)]
        [InlineData("C#", "XUnittest", false)]
        [InlineData("F#", "Console", false)]
        [InlineData("F#", "Lib", false)]
        [InlineData("F#", "Web", true)]
        [InlineData("F#", "Mstest", false)]
        [InlineData("F#", "XUnittest", false)]
        public void TemplateRestoresAndBuildsWithoutWarnings(
            string language,
            string projectType,
            bool useNuGetConfigForAspNet)
        {
            var rootPath = TestAssetsManager.CreateTestDirectory(identifier: $"{language}_{projectType}").Path;

            new TestCommand("dotnet") 
                .WithWorkingDirectory(rootPath)
                .Execute($"new --type {projectType} --lang {language}")
                .Should().Pass();

            if (useNuGetConfigForAspNet)
            {
                File.Copy("NuGet.tempaspnetpatch.config", Path.Combine(rootPath, "NuGet.Config"));
            }

            new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .Execute($"restore")
                .Should().Pass();

            var buildResult = new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("build")
                .Should().Pass()
                .And.NotHaveStdErr();
        }
    }
}
