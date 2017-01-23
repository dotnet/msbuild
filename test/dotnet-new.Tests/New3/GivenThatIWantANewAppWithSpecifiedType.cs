using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.New3.Tests
{
    public class GivenThatIWantANewAppWithSpecifiedType : New3TestBase
    {
        [Theory]
        [InlineData("C#", "console", false)]
        [InlineData("C#", "classlib", false)]
        [InlineData("C#", "mstest", false)]
        [InlineData("C#", "xunit", false)]
        [InlineData("C#", "web", true)]
        [InlineData("C#", "mvc", true)]
        [InlineData("C#", "webapi", true)]
        [InlineData("F#", "console", false)]
        [InlineData("F#", "classlib", false)]
        [InlineData("F#", "mstest", false)]
        [InlineData("F#", "xunit", false)]
        [InlineData("F#", "mvc", true)]
        public void TemplateRestoresAndBuildsWithoutWarnings(
            string language,
            string projectType,
            bool useNuGetConfigForAspNet)
        {
            string rootPath = TestAssetsManager.CreateTestDirectory(identifier: $"new3_{language}_{projectType}").Path;

            new TestCommand("dotnet") { WorkingDirectory = rootPath }
                .Execute($"new3 {projectType} -lang {language}")
                .Should().Pass();

            if (useNuGetConfigForAspNet)
            {
                File.Copy("NuGet.tempaspnetpatch.config", Path.Combine(rootPath, "NuGet.Config"));
            }

            string globalJsonPath = Path.Combine(rootPath, "global.json");
            Assert.True(File.Exists(globalJsonPath));
            Assert.Contains(Product.Version, File.ReadAllText(globalJsonPath));

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
