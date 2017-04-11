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
        [InlineData("C#", "xunit", false)]
        [InlineData("C#", "web", false)]
        [InlineData("C#", "mvc", false)]
        [InlineData("C#", "webapi", false)]
        // Uncomment the test below once https://github.com/dotnet/netcorecli-fsc/issues/92 is fixed.
        //[InlineData("F#", "console", false)]
        //[InlineData("F#", "classlib", false)]
        //[InlineData("F#", "mstest", false)]
        //[InlineData("F#", "xunit", false)]
        //[InlineData("F#", "mvc", true)]
        public void TemplateRestoresAndBuildsWithoutWarnings(
            string language,
            string projectType,
            bool useNuGetConfigForAspNet)
        {
            if (language == "F#" && !EnvironmentInfo.HasSharedFramework("netcoreapp1.0"))
            {
                // F# requires netcoreapp1.0 to be present in order to build
                // https://github.com/dotnet/netcorecli-fsc/issues/76
                return;
            }

            string rootPath = TestAssets.CreateTestDirectory(identifier: $"{language}_{projectType}").FullName;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"new {projectType} -lang {language} -o {rootPath} --debug:ephemeral-hive --no-restore")
                .Should().Pass();

            if (useNuGetConfigForAspNet)
            {
                var configFile = new FileInfo(Path.Combine(rootPath, "..", "..", "..", "..", "..", "NuGet.tempaspnetpatch.config"));
                File.Copy(configFile.FullName, Path.Combine(rootPath, "NuGet.Config"));
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
