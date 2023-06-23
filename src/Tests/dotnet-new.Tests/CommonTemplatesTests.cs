// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.New;
using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;
using TestLoggerFactory = Microsoft.NET.TestFramework.TestLoggerFactory;

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
            _logger = new TestLoggerFactory(log).CreateLogger(nameof(CommonTemplatesTests));
        }

        [Theory]
        [InlineData("global.json file", "globaljson", null)]
        [InlineData("global.json file", "globaljson", new[] { "--sdk-version", "6.0.200" })]
        [InlineData("global.json file", "globaljson", new[] { "--sdk-version", "6.0.200", "--roll-forward", "major" })]
        [InlineData("NuGet Config", "nugetconfig", null)]
        [InlineData("dotnet gitignore file", "gitignore", null)]
        [InlineData("Solution File", "sln", null)]
        [InlineData("Solution File", "solution", null)]
        [InlineData("Dotnet local tool manifest file", "tool-manifest", null)]
        [InlineData("Web Config", "webconfig", null)]
        [InlineData("EditorConfig file", "editorconfig", null)]
        [InlineData("EditorConfig file", "editorconfig", new[] { "--empty" })]
        [InlineData("MSBuild Directory.Build.props file", "buildprops", new[] { "--inherit" })]
        [InlineData("MSBuild Directory.Build.targets file", "buildtargets", new[] { "--inherit" })]
        public async void AllCommonItemsCreate(string expectedTemplateName, string templateShortName, string[]? args)
        {
            Dictionary<string, string> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            TestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            string itemName = expectedTemplateName.Replace(' ', '-').Replace('.', '-');

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                // squeshing snapshots by creating output unique for template (but not alias) and preventing item to have name by alias
                TemplateSpecificArgs = new[] { "-o", itemName, "-n", "item" }.Concat(args ?? Enumerable.Empty<string>()),
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                VerificationExcludePatterns = new[] { "*/stderr.txt", "*\\stderr.txt" },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                DoNotPrependTemplateNameToScenarioName = true,
                UniqueFor = expectedTemplateName.Equals("NuGet Config") ? UniqueForOption.OsPlatform : null,
            }
            .WithCustomEnvironment(environmentUnderTest)
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    .AddScrubber(sb => sb.UnixifyNewlines(), "out")
                    .AddScrubber((path, content) =>
                    {
                        if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                        {
                            content.UnixifyDirSeparators().Replace(expectedTemplateName, "%TEMPLATE_NAME%");
                        }
                    })
            );

            // globaljson is appending current sdk version. Due to the 'base' dotnet used to run test this version differs
            //  on dev and CI runs and possibly from the version within test host. Easiest is just to scrub it away
            if (expectedTemplateName.Equals("global.json file") && args == null)
            {
                string sdkVersionUnderTest = await new SdkInfoProvider().GetCurrentVersionAsync(default).ConfigureAwait(false);
                options.CustomScrubbers?.AddScrubber(sb => sb.Replace(sdkVersionUnderTest, "%CURRENT-VER%"), "json");
            }

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);
        }

        //
        // Sample of a custom verifier callback
        // To be uncommented in case editorconfig template will start to genearate dynamic content
        //

        //[Fact]
        //public async void EditorConfigTests_Default()
        //{
        //    TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "editorconfig")
        //    {
        //        SnapshotsDirectory = "Approvals",
        //        SettingsDirectory = _fixture.HomeDirectory,
        //    }
        //    .WithCustomDirectoryVerifier(async (content, contentFetcher) =>
        //    {
        //        await foreach (var (filePath, scrubbedContent) in contentFetcher.Value)
        //        {
        //            filePath.Replace(Path.DirectorySeparatorChar, '/').Should().BeEquivalentTo(@"editorconfig/.editorconfig");
        //            scrubbedContent.Should().Contain("dotnet_naming_rule");
        //            scrubbedContent.Should().Contain("dotnet_style_");
        //            scrubbedContent.Should().Contain("dotnet_naming_symbols");
        //        }
        //    });

        //    VerificationEngine engine = new VerificationEngine(_logger);
        //    await engine.Execute(options).ConfigureAwait(false);
        //}

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
                new { Template = consoleTemplateShortname,  Frameworks = new[] { null, "net6.0", "net7.0" } },
                new { Template = "classlib", Frameworks = new[] { null, "net6.0", "net7.0", "netstandard2.0", "netstandard2.1" } }
            };

            //features: top-level statements; nullables; implicit usings; filescoped namespaces

            string[] unsupportedLanguageVersions = { "1", "ISO-1" };
            //C# 12 is not supported yet - https://github.com/dotnet/sdk/issues/29195
            string?[] supportedLanguageVersions = { null, "ISO-2", "2", "3", "4", "5", "6", "7", "7.1", "7.2", "7.3", "8.0", "9.0", "10.0", "11.0", "11", /*"12",*/ "latest", "latestMajor", "default", "preview" };

            string?[] nullableSupportedInFrameworkByDefault = { null, "net6.0", "net7.0", "netstandard2.1" };
            string?[] implicitUsingsSupportedInFramework = { null, "net6.0", "net7.0" };
            string?[] fileScopedNamespacesSupportedFrameworkByDefault = { null, "net6.0", "net7.0" };

            string?[] nullableSupportedLanguages = { "8.0", "9.0", "10.0", "11.0", "11", /*"12",*/ "latest", "latestMajor", "default", "preview" };
            string?[] topLevelStatementSupportedLanguages = { null, "9.0", "10.0", "11", "11.0", /*"12",*/ "latest", "latestMajor", "default", "preview" };
            string?[] implicitUsingsSupportedLanguages = { null, "10.0", "11.0", "11", /*"12",*/ "latest", "latestMajor", "default", "preview" };
            string?[] fileScopedNamespacesSupportedLanguages = { "10.0", "11.0", "11", /*"12",*/ "latest", "latestMajor", "default", "preview" };

            string?[] supportedLangs = { null, "C#", "F#", "VB" };

            foreach (var template in templatesToTest)
            {
                foreach (string? langVersion in unsupportedLanguageVersions.Concat(supportedLanguageVersions))
                {
                    IEnumerable<string?> frameworks = template.Frameworks;
                    IEnumerable<string?> langs = new string?[] { null };
                    if (langVersion == null)
                    {
                        langs = supportedLangs;
                    }

                    foreach (string? framework in frameworks)
                    {
                        // Skip tests due to https://github.com/dotnet/templating/issues/5668#issuecomment-1327438284
                        if (framework == "net6.0" && double.TryParse(langVersion, out double lv) && lv >= 11)
                        {
                            continue;
                        }

                        foreach (string? lang in langs)
                        {
                            yield return CreateParams(template.Template, langVersion, lang, framework, false)!;
                            var testParams = CreateParams(template.Template, langVersion, lang, framework, true);
                            if (testParams != null)
                            {
                                yield return testParams;
                            }
                        }
                    }
                }
            }

            object?[]? CreateParams(string templateName, string? langVersion, string? lang, string? framework, bool forceDisableTopLevel)
            {
                bool supportsTopLevel = topLevelStatementSupportedLanguages.Contains(langVersion);

                // If forceDisableTopLevel is requested - then generate params only if it makes sense - for C# console project of a version that
                //  supports top level statements. Otherwise it doesn't make sense to test this overwritting functionality
                if ((!supportsTopLevel || !templateName.Equals(consoleTemplateShortname) || (lang != null && lang != "C#")) && forceDisableTopLevel)
                {
                    return null;
                }

                return new object?[]
                {
                    templateName,
                    // buildPass
                    supportedLanguageVersions.Contains(langVersion),
                    framework,
                    langVersion,
                    // langVersionUnsupported
                    unsupportedLanguageVersions.Contains(langVersion),
                    lang,
                    // supportsNullable
                    nullableSupportedLanguages.Contains(langVersion)
                     || langVersion == null && nullableSupportedInFrameworkByDefault.Contains(framework),
                    supportsTopLevel,
                    forceDisableTopLevel,
                    // supportsImplicitUsings
                    implicitUsingsSupportedLanguages.Contains(langVersion) && implicitUsingsSupportedInFramework.Contains(framework),
                    // supportsFileScopedNs
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
            bool langVersionUnsupported,
            string? language,
            bool supportsNullable,
            bool supportsTopLevel,
            bool forceDisableTopLevel,
            bool supportsImplicitUsings,
            bool supportsFileScopedNs)
        {
            // "net8.0";
            string currentDefaultFramework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";

            string workingDir = CreateTemporaryFolder(folderName: $"{name}-{langVersion ?? "null"}-{framework ?? "null"}");
            string outputDir = "MyProject";
            string projName = name;

            List<string> args = new() { "-o", outputDir };
            // VB build would fail for name 'console' (root namespace would conflict with BCL namespace)
            if (language?.Equals("VB") == true && name.Equals("console"))
            {
                projName = "vb-console";
                args.Add("-n");
                args.Add(projName);
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
            if (!string.IsNullOrWhiteSpace(language))
            {
                args.Add("--language");
                args.Add(language);
            }
            if (!buildPass)
            {
                args.Add("--no-restore");
            }
            if (forceDisableTopLevel)
            {
                args.Add("--use-program-main");
                supportsTopLevel = false;
            }

            string extension = language switch
            {
                "F#" => "fsproj",
                "VB" => "vbproj",
                _ => "csproj"
            };

            string projectDir = Path.Combine(workingDir, outputDir);
            string finalProjectName = Path.Combine(projectDir, $"{projName}.{extension}");

            Dictionary<string, string> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            TestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: name)
            {
                TemplateSpecificArgs = args,
                SnapshotsDirectory = "Approvals",
                OutputDirectory = workingDir,
                SettingsDirectory = _fixture.HomeDirectory,
                VerifyCommandOutput = true,
                DoNotPrependTemplateNameToScenarioName = false,
                DoNotAppendTemplateArgsToScenarioName = true,
                ScenarioName =
                    $"Nullable-{supportsNullable}#TopLevel-{supportsTopLevel}#ImplicitUsings-{supportsImplicitUsings}#FileScopedNs-{supportsFileScopedNs}"
                    + (string.IsNullOrEmpty(framework) ? string.Empty : $"#Framework-{framework}")
                    + '#' + (language == null ? "cs" : language.Replace('#', 's').ToLower())
                    + (langVersion == null ? "#NoLangVer" : (langVersionUnsupported ? "#UnsuportedLangVer" : null)),
                VerificationExcludePatterns = new[] { "*/stderr.txt", "*\\stderr.txt" },
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
            }
            .WithCustomEnvironment(environmentUnderTest)
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    .AddScrubber(sb => sb.Replace($"<LangVersion>{langVersion}</LangVersion>", "<LangVersion>%LANG%</LangVersion>"))
                    .AddScrubber(sb => sb.Replace($"<TargetFramework>{framework ?? currentDefaultFramework}</TargetFramework>", "<TargetFramework>%FRAMEWORK%</TargetFramework>"))
                    .AddScrubber(sb => sb.Replace(finalProjectName, "%PROJECT_PATH%").UnixifyDirSeparators().ScrubByRegex("(^  Restored .* \\()(.*)(\\)\\.)", "$1%DURATION%$3", RegexOptions.Multiline), "txt")
            );

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options).ConfigureAwait(false);

            if (buildPass)
            {
                new DotnetBuildCommand(_log, "MyProject")
                    .WithWorkingDirectory(workingDir)
                    .Execute()
                    .Should()
                    .Pass()
                    .And.NotHaveStdErr();
            }

            Directory.Delete(workingDir, true);
        }

        #endregion
    }
}
