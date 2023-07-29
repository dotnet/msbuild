// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using NuGet.Packaging;

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
            _logger = new TestLoggerFactory(log).CreateLogger(nameof(DotnetClassTemplateTests));
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
        [InlineData("enum", "", "netstandard2.0")]
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
                           "Warning: Failed to evaluate bind symbol \'evaluatedLangVersion\', it will be skipped.",
                           string.Empty);

                       content.ScrubAndReplace("\n", string.Empty);
                   }
               }));

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);

            ValidateInstantiatedProject(workingDir);
        }

        [Theory]
        [InlineData("class")]
        [InlineData("class", "latest", "net7.0")]
        [InlineData("class", "16", "net6.0")]
        [InlineData("class", "15.3", "netstandard2.0")]
        [InlineData("enum")]
        [InlineData("enum", "16", "net6.0")]
        [InlineData("enum", "latest", "net7.0")]
        [InlineData("enum", "15.3", "netstandard2.0")]
        [InlineData("structure")]
        [InlineData("structure", "latest")]
        [InlineData("struct", "16", "net6.0")]
        [InlineData("structure", "15.3", "netstandard2.0", "CustomFileName")]
        [InlineData("interface")]
        [InlineData("interface", "16", "net7.0")]
        [InlineData("interface", "latest", "net6.0")]
        [InlineData("interface", "15.3", "netstandard2.0")]
        [InlineData("module")]
        [InlineData("module", "16", "net7.0")]
        [InlineData("module", "latest", "net6.0")]
        [InlineData("module", "15.3", "netstandard2.0")]
        [InlineData("module", "15.5", "netstandard2.0", "CustomFileName")]
        public async void DotnetVisualBasicClassTemplatesTest(
            string templateShortName,
            string langVersion = "",
            string targetFramework = "",
            string fileName = "")
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
                TemplateSpecificArgs = new[] { "--name", string.IsNullOrWhiteSpace(fileName) ? "TestItem1" : fileName, "--language", "VB" },
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
                           "Warning: Failed to evaluate bind symbol \'evaluatedLangVersion\', it will be skipped.",
                           string.Empty);

                       content.ScrubAndReplace("\n", string.Empty);
                   }
               }));

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);

            ValidateInstantiatedProject(workingDir);
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

        private void ValidateInstantiatedProject(string workingDir)
        {
            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Delete(workingDir, true);
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
