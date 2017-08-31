// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.New.Tests
{
    public class GivenThatIWantANewAppWithSpecifiedType : TestBase
    {
        [Theory]
        [InlineData("C#", "console", false, false)]
        [InlineData("C#", "classlib", false, false)]
        [InlineData("C#", "mstest", false, false)]
        [InlineData("C#", "xunit", false, false)]
        [InlineData("C#", "web", true, false)]
        [InlineData("C#", "mvc", true, false)]
        [InlineData("C#", "webapi", true, false)]
        [InlineData("C#", "angular", false, true)]
        [InlineData("C#", "react", false, true)]
        [InlineData("C#", "reactredux", false, true)]
        [InlineData("F#", "console", false, false)]
        // re-enable when this bug is resolved: https://github.com/dotnet/cli/pull/7554
        //[InlineData("F#", "classlib", false, false)]
        [InlineData("F#", "mstest", false, false)]
        [InlineData("F#", "xunit", false, false)]
        [InlineData("F#", "mvc", true, false)]
        [InlineData("VB", "console", false, false)]
        [InlineData("VB", "classlib", false, false)]
        [InlineData("VB", "mstest", false, false)]
        [InlineData("VB", "xunit", false, false)]
        public void TemplateRestoresAndBuildsWithoutWarnings(
            string language,
            string projectType,
            bool useNuGetConfigForAspNet,
            bool skipSpaWebpackSteps)
        {
            string rootPath = TestAssets.CreateTestDirectory(identifier: $"{language}_{projectType}").FullName;
            string noRestoreDirective = "--no-restore";

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"new {projectType} -lang {language} -o {rootPath} --debug:ephemeral-hive {noRestoreDirective}")
                .Should().Pass();

            if (useNuGetConfigForAspNet)
            {
                var configFile = new FileInfo(Path.Combine(rootPath, "..", "..", "..", "..", "..", "NuGet.tempaspnetpatch.config"));
                File.Copy(configFile.FullName, Path.Combine(rootPath, "NuGet.Config"));
            }

            if (skipSpaWebpackSteps)
            {
                // Not all CI machines have Node installed, so the build would fail if we tried
                // to run Webpack. Bypass this by making it appear that Webpack already ran.
                Directory.CreateDirectory(Path.Combine(rootPath, "wwwroot", "dist"));
            }

            new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .Execute($"restore")
                .Should().Pass();

            var buildResult = new TestCommand("dotnet")
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("build --no-restore")
                .Should().Pass()
                .And.NotHaveStdErr();
        }
    }
}
