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
        [InlineData("C#", "console", false)]
        [InlineData("C#", "classlib", false)]
        [InlineData("C#", "mstest", false)]
        [InlineData("C#", "nunit", false)]
        [InlineData("C#", "xunit", false)]
        [InlineData("C#", "web", false)]
        [InlineData("C#", "mvc", false)]
        [InlineData("C#", "webapi", false)]
        [InlineData("C#", "angular", true)]
        [InlineData("C#", "react", true)]
        [InlineData("C#", "reactredux", true)]
        [InlineData("F#", "console", false)]
        // re-enable when this bug is resolved: https://github.com/dotnet/cli/issues/7574
        //[InlineData("F#", "classlib", false)]
        [InlineData("F#", "mstest", false)]
        [InlineData("F#", "nunit", false)]
        [InlineData("F#", "xunit", false)]
        [InlineData("F#", "mvc", false)]
        [InlineData("VB", "console", false)]
        [InlineData("VB", "classlib", false)]
        [InlineData("VB", "mstest", false)]
        [InlineData("VB", "nunit", false)]
        [InlineData("VB", "xunit", false)]
        public void TemplateRestoresAndBuildsWithoutWarnings(
            string language,
            string projectType,
            bool skipSpaWebpackSteps)
        {
            string rootPath = TestAssets.CreateTestDirectory(identifier: $"{language}_{projectType}").FullName;
            string noRestoreDirective = "--no-restore";

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"new {projectType} -lang {language} -o {rootPath} --debug:ephemeral-hive {noRestoreDirective}")
                .Should().Pass();

            if (skipSpaWebpackSteps)
            {
                // Not all CI machines have Node installed, so the build would fail if we tried
                // to run Webpack. Bypass this by making it appear that Webpack already ran.
                Directory.CreateDirectory(Path.Combine(rootPath, "wwwroot", "dist"));
                Directory.CreateDirectory(Path.Combine(rootPath, "ClientApp", "node_modules"));
                Directory.CreateDirectory(Path.Combine(rootPath, "node_modules"));
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
