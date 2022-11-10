// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class CommonTemplatesTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _fixture;
        private readonly ITestOutputHelper _log;
        private readonly ILogger _logger;

        public CommonTemplatesTests(SharedHomeDirectory fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
            _logger = new XunitLoggerProvider(log).CreateLogger("TestRun");
        }

        [Theory]
        [InlineData("Console App", "console")]
        [InlineData("Console App", "console", "C#")]
        [InlineData("Console App", "console", "F#")]
        [InlineData("Console App", "console", "VB")]
        [InlineData("Console App", "console", "C#", "net7.0")]
        [InlineData("Console App", "console", "F#", "net7.0")]
        [InlineData("Console App", "console", "VB", "net7.0")]

        [InlineData("Class Library", "classlib")]
        [InlineData("Class Library", "classlib", "C#")]
        [InlineData("Class Library", "classlib", "F#")]
        [InlineData("Class Library", "classlib", "VB")]
        [InlineData("Class Library", "classlib", "C#", "net7.0")]
        [InlineData("Class Library", "classlib", "F#", "net7.0")]
        [InlineData("Class Library", "classlib", "VB", "net7.0")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.0")]
        public async void AllCommonProjectsCreateRestoreAndBuild(string expectedTemplateName, string templateShortName, string? language = null, string? framework = null, string? langVersion = null)
        {
            string workingDir = CreateTemporaryFolder(folderName: $"{templateShortName}-{language?.Replace("#", "Sharp") ?? "null"}-{framework ?? "null"}");
            string extension = language switch
            {
                "F#" => "fsproj",
                "VB" => "vbproj",
                _ => "csproj"
            };

            string projectName = "sample-project-name";
            string projectDir = Path.Combine(workingDir, templateShortName);
            string finalProjectName = Path.Combine(projectDir, $"{projectName}.{extension}");
            Console.WriteLine($"Expected project location: {finalProjectName}");

            List<string> args = new() { "-n", projectName };
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplateSpecificArgs = args,
                SnapshotsDirectory = "Approvals",
                OutputDirectory = workingDir,
                VerifyCommandOutput = true,
                VerificationExcludePatterns = new[] { "*.cs", "*.fs", "*.vb", "*.*proj" },
                DoNotPrependCallerMethodNameToScenarioName = false,
                DoNotAppendTemplateArgsToScenarioName = true,
                DoNotPrependTemplateNameToScenarioName = true,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
            }
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    .AddScrubber(sb =>
                    {
                        const string projectPathTag = "%PROJECT_NAME%";
                        sb.Replace(expectedTemplateName, "%TEMPLATE_NAME%").Replace(finalProjectName, projectPathTag);
                        string pattern = "(^  Restored " + Regex.Escape(projectPathTag) + " \\()(.*)(\\)\\.)";
                        string res = sb.ToString();
                        res = Regex.Replace(res, pattern, "$1%DURATION%$3", RegexOptions.Multiline);
                        sb.Clear();
                        sb.Append(res);
                    }));

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);

            new DotnetRestoreCommand(_log)
                .WithWorkingDirectory(projectDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(projectDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }

        [Theory]
        [InlineData("Console App", "console")]
        [InlineData("Console App", "console", "C#")]
        [InlineData("Console App", "console", "F#")]
        [InlineData("Console App", "console", "VB")]
        [InlineData("Console App", "console", "C#", "net7.0")]
        [InlineData("Console App", "console", "F#", "net7.0")]
        [InlineData("Console App", "console", "VB", "net7.0")]
        [InlineData("Console Application", "console", "C#", "net5.0")]
        [InlineData("Console Application", "console", "F#", "net5.0")]
        [InlineData("Console Application", "console", "VB", "net5.0")]
        [InlineData("Console Application", "console", "C#", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "F#", "netcoreapp3.1")]
        [InlineData("Console Application", "console", "VB", "netcoreapp3.1")]

        [InlineData("Class Library", "classlib")]
        [InlineData("Class Library", "classlib", "C#")]
        [InlineData("Class Library", "classlib", "F#")]
        [InlineData("Class Library", "classlib", "VB")]
        [InlineData("Class Library", "classlib", "C#", "net7.0")]
        [InlineData("Class Library", "classlib", "F#", "net7.0")]
        [InlineData("Class Library", "classlib", "VB", "net7.0")]
        [InlineData("Class library", "classlib", "C#", "net5.0")]
        [InlineData("Class library", "classlib", "F#", "net5.0")]
        [InlineData("Class library", "classlib", "VB", "net5.0")]
        [InlineData("Class library", "classlib", "C#", "netcoreapp3.1")]
        [InlineData("Class library", "classlib", "F#", "netcoreapp3.1")]
        [InlineData("Class library", "classlib", "VB", "netcoreapp3.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.1")]
        [InlineData("Class Library", "classlib", "C#", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "VB", "netstandard2.0")]
        [InlineData("Class Library", "classlib", "F#", "netstandard2.0")]
        public async void AllCommonProjectsCreate_NoRestore(string expectedTemplateName, string templateShortName, string? language = null, string? framework = null)
        {
            List<string> args = new() { "--no-restore" };
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                TemplateSpecificArgs = args,
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                VerificationIncludePatterns = new[] { "*.txt" },
                DoNotAppendTemplateArgsToScenarioName = true,
                SettingsDirectory = _fixture.HomeDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
            }
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    .AddScrubber(sb => sb.Replace(expectedTemplateName, "%TEMPLATE_NAME%"))
            );

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);
        }

        [Theory]
        [InlineData("dotnet gitignore file", "gitignore")]
        [InlineData("global.json file", "globaljson")]
        [InlineData("NuGet Config", "nugetconfig")]
        [InlineData("Solution File", "sln")]
        [InlineData("Solution File", "solution")]
        [InlineData("Dotnet local tool manifest file", "tool-manifest")]
        [InlineData("Web Config", "webconfig")]
        public async void AllCommonItemsCreate(string expectedTemplateName, string templateShortName)
        {
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                VerificationIncludePatterns = new[] { "*.txt" },
                DoNotAppendTemplateArgsToScenarioName = true,
                SettingsDirectory = _fixture.HomeDirectory,
                DoNotPrependTemplateNameToScenarioName = true,
            }
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    .AddScrubber((path, content) =>
                    {
                        if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                        {
                            content.Replace(expectedTemplateName, "%TEMPLATE_NAME%");
                        }
                    })
            );

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);
        }

        [Fact]
        public async void EditorConfigTests_Empty()
        {
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "editorconfig")
            {
                TemplateSpecificArgs = new[] { "--empty" },
                SnapshotsDirectory = "Approvals",
                SettingsDirectory = _fixture.HomeDirectory,
                VerifyCommandOutput = true,
            };

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);
        }

        [Fact]
        public async void EditorConfigTests_Default()
        {
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "editorconfig")
            {
                SnapshotsDirectory = "Approvals",
                SettingsDirectory = _fixture.HomeDirectory,
            }
            .WithCustomDirectoryVerifier(async (content, contentFetcher) =>
            {
                await foreach (var (filePath, scrubbedContent) in contentFetcher.Value)
                {
                    filePath.Replace(Path.DirectorySeparatorChar, '/').Should().BeEquivalentTo(@"editorconfig/.editorconfig");
                    scrubbedContent.Should().Contain("dotnet_naming_rule");
                    scrubbedContent.Should().Contain("dotnet_style_");
                    scrubbedContent.Should().Contain("dotnet_naming_symbols");
                }
            });

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);
        }

        [Theory]
        [InlineData(
            "globaljson",
            "--sdk-version",
            "5.0.200")]
        [InlineData(
            "globaljson",
            "--sdk-version",
            "5.0.200",
            "--roll-forward",
            "major")]
        public async void GlobalJsonTests(params string[] parameters)
        {
            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: parameters[0])
            {
                TemplateSpecificArgs = parameters[1..],
                SnapshotsDirectory = "Approvals",
                SettingsDirectory = _fixture.HomeDirectory,
                VerifyCommandOutput = true,
            };

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);
        }

        [Fact]
        public void NuGetConfigPermissions()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //runs only on Unix
                return;
            }

            string templateShortName = "nugetconfig";
            string expectedTemplateName = "NuGet Config";
            string workingDir = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, templateShortName)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($@"The template ""{expectedTemplateName}"" was created successfully.");

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = "/bin/sh",
                Arguments = "-c \"ls -la\"",
                WorkingDirectory = workingDir
            });

            new Command(process)
                .WorkingDirectory(workingDir)
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutMatching("^-rw-------.*nuget.config$", RegexOptions.Multiline);

            Directory.Delete(workingDir, true);
        }

        #region Project templates language features tests

        /// <summary>
        /// Creates all possible combinations for supported templates, language versions and frameworks.
        /// </summary>
        public static IEnumerable<object?[]> FeaturesSupport_Data()
        {
            const string consoleTemplateShortname = "console";

            var templatesToTest = new[]
            {
                new { Template = consoleTemplateShortname,  Frameworks = new[] { null, "net7.0" } },
                new { Template = "classlib", Frameworks = new[] { null, "net7.0", "netstandard2.0", "netstandard2.1" } }
            };

            //features: top-level statements; nullables; implicit usings; filescoped namespaces

            string[] unsupportedLanguageVersions = { "1", "ISO-1" };
            string?[] supportedLanguageVersions = { null, "ISO-2", "2", "3", "4", "5", "6", "7", "7.1", "7.2", "7.3", "8.0", "9.0", "10.0", "11.0", "11", "latest", "latestMajor", "default", "preview" };

            string?[] nullableSupportedInFrameworkByDefault = { null, "net7.0", "netstandard2.1" };
            string?[] implicitUsingsSupportedInFramework = { null, "net7.0" };
            string?[] fileScopedNamespacesSupportedFrameworkByDefault = { null, "net7.0" };

            string?[] nullableSupportedLanguages = { "8.0", "9.0", "10.0", "11.0", "11", "latest", "latestMajor", "default", "preview" };
            string?[] topLevelStatementSupportedLanguages = { null, "9.0", "10.0", "11", "11.0", "latest", "latestMajor", "default", "preview" };
            string?[] implicitUsingsSupportedLanguages = { null, "10.0", "11.0", "11", "latest", "latestMajor", "default", "preview" };
            string?[] fileScopedNamespacesSupportedLanguages = { "10.0", "11.0", "11", "latest", "latestMajor", "default", "preview" };

            foreach (var template in templatesToTest)
            {
                foreach (string? langVersion in unsupportedLanguageVersions.Concat(supportedLanguageVersions))
                {
                    foreach (string? framework in template.Frameworks)
                    {
                        yield return CreateParams(template.Template, langVersion, framework, false)!;
                        var testParams = CreateParams(template.Template, langVersion, framework, true);
                        if (testParams != null)
                        {
                            yield return testParams;
                        }
                    }
                }
            }

            object?[]? CreateParams(string templateName, string? langVersion, string? framework, bool forceDisableTopLevel)
            {
                bool supportsTopLevel = topLevelStatementSupportedLanguages.Contains(langVersion);

                if ((!supportsTopLevel || !templateName.Equals(consoleTemplateShortname)) && forceDisableTopLevel)
                {
                    return null;
                }

                return new object?[]
                {
                    templateName,
                    supportedLanguageVersions.Contains(langVersion),
                    framework,
                    langVersion,
                    nullableSupportedLanguages.Contains(langVersion)
                    || langVersion == null && nullableSupportedInFrameworkByDefault.Contains(framework),
                    supportsTopLevel,
                    forceDisableTopLevel,
                    implicitUsingsSupportedLanguages.Contains(langVersion) && implicitUsingsSupportedInFramework.Contains(framework),
                    fileScopedNamespacesSupportedLanguages.Contains(langVersion)
                    || langVersion == null && fileScopedNamespacesSupportedFrameworkByDefault.Contains(framework),
                };
            }
        }

        [Theory]
        //creates all possible combinations for supported templates, language versions and frameworks
#pragma warning disable CA1825 // Avoid zero-length array allocations. https://github.com/dotnet/sdk/issues/28672
        [MemberData(nameof(FeaturesSupport_Data))]
#pragma warning restore CA1825 // Avoid zero-length array allocations.
        public async void FeaturesSupport(
            string name,
            bool buildPass,
            string? framework,
            string? langVersion,
            bool supportsNullable,
            bool supportsTopLevel,
            bool forceDisableTopLevel,
            bool supportsImplicitUsings,
            bool supportsFileScopedNs)
        {
            var v = FeaturesSupport_Data().ToList();
            v.Should().NotBeEmpty();

            const string currentDefaultFramework = "net7.0";
            //string currentDefaultFramework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";

            string workingDir = CreateTemporaryFolder(folderName: $"{name}-{langVersion ?? "null"}-{framework ?? "null"}");

            List<string> args = new() { "-o", "MyProject" };
            if (!string.IsNullOrWhiteSpace(framework))
            {
                args.Add("--framework");
                args.Add(framework);
            }
            if (!string.IsNullOrWhiteSpace(langVersion))
            {
                args.Add("--langVersion");
                args.Add(langVersion);
            }

            if (forceDisableTopLevel)
            {
                args.Add("--use-program-main");
                supportsTopLevel = false;
            }

            Dictionary<string, string> environmentUnderTest = new();
            TestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: name)
            {
                TemplateSpecificArgs = args,
                SnapshotsDirectory = "Approvals",
                OutputDirectory = workingDir,
                SettingsDirectory = _fixture.HomeDirectory,
                DoNotPrependTemplateNameToScenarioName = false,
                DoNotAppendTemplateArgsToScenarioName = true,
                ScenarioName = !buildPass ?
                    "OutOfSupport" :
                    $"Nullable-{supportsNullable}#TopLevel-{supportsTopLevel}#ImplicitUsings-{supportsImplicitUsings}#FileScopedNs-{supportsFileScopedNs}" + (langVersion == null ? "#NoLang" : null),
                VerificationExcludePatterns = buildPass ? null : new[] { "*" },
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
            }
            .WithCustomEnvironment(environmentUnderTest)
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    //Todo: add extension here (once fixed in templating)
                    .AddScrubber(sb => sb.Replace($"<LangVersion>{langVersion}</LangVersion>", "<LangVersion>%LANG%</LangVersion>"))
                    .AddScrubber(sb => sb.Replace($"<TargetFramework>{framework ?? currentDefaultFramework}</TargetFramework>", "<TargetFramework>%FRAMEWORK%</TargetFramework>"), "csproj")
            );

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);

            CommandResult buildResult = new DotnetBuildCommand(_log, "MyProject")
                .WithWorkingDirectory(workingDir)
                .Execute();

            if (buildPass)
            {
                buildResult.Should().ExitWith(0).And.NotHaveStdErr();
            }
            else
            {
                buildResult.Should().Fail();
            }
            Directory.Delete(workingDir, true);
        }

        #endregion
    }
}
