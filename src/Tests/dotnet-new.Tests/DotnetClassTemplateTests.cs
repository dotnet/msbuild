// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Packaging;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class DotnetClassTemplateTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _fixture;
        private readonly ITestOutputHelper _log;
        private readonly ILogger _logger;

        public DotnetClassTemplateTests(SharedHomeDirectory fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
            _logger = new XunitLoggerProvider(log)
                .CreateLogger("TestRun");
        }

        [Theory]
        [InlineData("class")]
        [InlineData("class", "preview", "net7.0")]
        [InlineData("class", "10.0", "net6.0")]
        [InlineData("class", "9.0", "netstandard2.0")]
        [InlineData("interface")]
        [InlineData("interface", "10.0", "net6.0")]
        [InlineData("interface", "9", "netstandard2.0")]
        [InlineData("record")]
        [InlineData("record", "10", "net6.0")]
        [InlineData("record", "9.0")]
        [InlineData("record", "8.0", "netstandard2.0")]
        [InlineData("struct")]
        [InlineData("struct", "10")]
        [InlineData("struct", "10", "net6.0")]
        [InlineData("struct", "9.0", "netstandard2.0")]
        [InlineData("enum")]
        [InlineData("enum", "10", "net6.0")]
        [InlineData("enum", "", "net7.0")]
        [InlineData("enum", "9.0", "netstandard2.0")]
        public async void DotnetCSharpClassTemplatesTest(
            string templateShortName,
            string langVersion = "",
            string targetFramework = "")
        {
            // prevents logging a welcome message from sdk installation
            Dictionary<string, string> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            TestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            string folderName = GetFolderName(templateShortName, langVersion, targetFramework);
            string workingDir = CreateTemporaryFolder($"{nameof(DotnetCSharpClassTemplatesTest)}.{folderName}");
            string projectName = CreateTestProject(workingDir, langVersion, targetFramework);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                TemplateSpecificArgs = new[] { "--name", "TestItem1" },
                VerificationExcludePatterns = new[]
                {
                    "*/stderr.txt",
                    "*\\stderr.txt",
                    // restored files in obj folder
                    $"*{projectName}.csproj.*",
                    "*project.*.*"
                },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                DoNotAppendTemplateArgsToScenarioName = true,
                DoNotPrependTemplateNameToScenarioName = true,
                ScenarioName = folderName,
                OutputDirectory = workingDir,
                EnsureEmptyOutputDirectory = false
            }
            .WithCustomEnvironment(environmentUnderTest)
            .WithCustomScrubbers(
               ScrubbersDefinition.Empty
               .AddScrubber((path, content) =>
               {
                   if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                   {
                       content
                       .UnixifyNewlines()
                       .ScrubAndReplace(
                           "Warning: Failed to evaluate bind symbol \'langVersion\', it will be skipped.",
                           string.Empty);

                       content.ScrubAndReplace("\n", string.Empty);
                   }
               }));

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options)
                .ConfigureAwait(false);
        }

        [Theory]
        [InlineData("class")]
        [InlineData("class", "11.0", "net7.0")]
        [InlineData("class", "10.0", "net6.0")]
        [InlineData("class", "9.0", "netstandard2.0")]
        [InlineData("enum")]
        [InlineData("enum", "10", "net6.0")]
        [InlineData("enum", "", "net7.0")]
        [InlineData("enum", "9.0", "netstandard2.0")]
        [InlineData("struct")]
        [InlineData("struct", "10")]
        [InlineData("struct", "10", "net6.0")]
        [InlineData("struct", "9.0", "netstandard2.0")]
        [InlineData("interface")]
        [InlineData("interface", "11.0", "net7.0")]
        [InlineData("interface", "10.0", "net6.0")]
        [InlineData("interface", "9", "netstandard2.0")]
        public async void DotnetVisualBasicClassTemplatesTest(
            string templateShortName,
            string langVersion = "",
            string targetFramework = "")
        {
            // prevents logging a welcome message from sdk installation
            Dictionary<string, string> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            TestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            string folderName = GetFolderName(templateShortName, langVersion, targetFramework);
            string workingDir = CreateTemporaryFolder($"{nameof(DotnetVisualBasicClassTemplatesTest)}.{folderName}");
            string projectName = CreateTestProject(workingDir, langVersion, targetFramework, "VB");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                TemplateSpecificArgs = new[] { "--name", "TestItem1", "--language", "VB" },
                VerificationExcludePatterns = new[]
                {
                    "*/stderr.txt",
                    "*\\stderr.txt",
                    // restored files in obj folder
                    $"*{projectName}.vbproj.*",
                    "*project.*.*"
                },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                DoNotAppendTemplateArgsToScenarioName = true,
                DoNotPrependTemplateNameToScenarioName = true,
                ScenarioName = folderName,
                OutputDirectory = workingDir,
                EnsureEmptyOutputDirectory = false
            }
            .WithCustomEnvironment(environmentUnderTest)
            .WithCustomScrubbers(
               ScrubbersDefinition.Empty
               .AddScrubber((path, content) =>
               {
                   if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                   {
                       content
                       .UnixifyNewlines()
                       .ScrubAndReplace(
                           "Warning: Failed to evaluate bind symbol \'langVersion\', it will be skipped.",
                           string.Empty);

                       content.ScrubAndReplace("\n", string.Empty);
                   }
               }));

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options)
                .ConfigureAwait(false);
        }

        private string CreateTestProject(
            string workingDir,
            string langVersion,
            string targetFramework,
            string language = "")
        {
            IDictionary<string, string> languageToProjectExtMap = new Dictionary<string, string>
            {
                { "VB", ".vbproj" },
                { "", ".csproj" }
            };

            IDictionary<string, string> languageToClassExtMap = new Dictionary<string, string>
            {
                { "VB", ".vb" },
                { "", ".cs" }
            };

            IList<string> projectArgs = new List<string>() { "classlib", "-o", workingDir, "--name", "ClassLib" };
            if (!string.IsNullOrEmpty(langVersion))
            {
                projectArgs.AddRange(new[] { "--langVersion", langVersion });
            }
            if (!string.IsNullOrEmpty(targetFramework))
            {
                projectArgs.AddRange(new[] { "--framework", targetFramework });
            }
            if (!string.IsNullOrEmpty(language))
            {
                projectArgs.AddRange(new[] { "--language", language });
            }

            new DotnetNewCommand(Log, projectArgs.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            foreach (string classFile in Directory.GetFiles(workingDir, $"*{languageToClassExtMap[language]}"))
            {
                File.Delete(classFile);
            }

            return Path.GetFileNameWithoutExtension(Directory
                .GetFiles(workingDir, $"*{languageToProjectExtMap[language]}")?.FirstOrDefault() ?? string.Empty);
        }

        private string GetFolderName(string templateShortName, string langVersion, string targetFramework)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{templateShortName}");

            if (!string.IsNullOrEmpty(langVersion))
            {
                sb.Append($".langVersion={langVersion}");
            }

            if (!string.IsNullOrEmpty(targetFramework))
            {
                sb.Append($".targetFramework={targetFramework}");
            }

            return sb.ToString();
        }
    }
}
