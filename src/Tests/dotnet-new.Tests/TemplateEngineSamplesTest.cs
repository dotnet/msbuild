// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using EmptyFiles;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [Collection("Verify Tests")]
    public class TemplateEngineSamplesTest : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ILogger _log;

        public TemplateEngineSamplesTest(SharedHomeDirectory sharedHome, ITestOutputHelper log)
            : base(log)
        {
            _log = new TestLoggerFactory(log).CreateLogger(nameof(CommonTemplatesTests));
            _sharedHome = sharedHome;
            _sharedHome.InstallPackage("Microsoft.TemplateEngine.Samples");
        }

        [Theory]
        [InlineData("01-basic-template", "sample01", null, "no args")]
        [InlineData("02-add-parameters", "sample02", new[] { "--copyrightName", "Test Copyright", "--title", "Test Title" }, "text args")]
        [InlineData("03-optional-page", "sample03", new[] { "--EnableContactPage", "true" }, "optional content included")]
        [InlineData("03-optional-page", "sample03", null, "optional content excluded")]
        [InlineData("04-parameter-from-list", "sample04", new[] { "--BackgroundColor", "dimgray" }, "the choice parameter")]
        [InlineData("05-multi-project", "sample05", new[] { "--includetest", "true" }, "the optional test project included")]
        [InlineData("05-multi-project", "sample05", new[] { "--includetest", "false" }, "the optional test project excluded")]
        [InlineData("06-console-csharp-fsharp", "sample06", null, "multiple languages supported. This one creates a template according to the default option - C#")]
        [InlineData("06-console-csharp-fsharp", "sample06", new[] { "--language", "F#" }, "multiple languages supported. This one creates F# language template")]
        [InlineData("07-param-with-custom-short-name", "sample07", null, "customised parameter name")]
        [InlineData("09-replace-onlyif-after", "sample09", new[] { "--backgroundColor", "grey" }, "replacing with onlyif condition")]
        [InlineData("10-symbol-from-date", "sample10", null, "usage of date generator")]
        [InlineData("11-change-string-casing", "sample11", null, "usage of casing generator")]
        [InlineData("13-constant-value", "sample13", null, "replacing of constant value")]
        [InlineData("15-computed-symbol", "sample15", null, "usage computed symbols")]
        [InlineData("16-string-value-transform", "sample16", null, "usage of derived parameter")]
        public async void TemplateEngineSamplesProjectTest(
            string folderName,
            string shortName,
            string[] arguments,
            string caseDescription)
        {
            _log.LogInformation($"Template with {caseDescription}");
            Dictionary<string, string> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            TestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);
            FileExtensions.AddTextExtension(".cshtml");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: shortName)
            {
                TemplateSpecificArgs = arguments ?? Enumerable.Empty<string>(),
                VerifyCommandOutput = true,
                SnapshotsDirectory = "Approvals",
                SettingsDirectory = _sharedHome.HomeDirectory,
                DoNotAppendTemplateArgsToScenarioName = true,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                DoNotPrependCallerMethodNameToScenarioName = true,
                ScenarioName = $"{folderName.Substring(folderName.IndexOf("-") + 1)}{GetScenarioName(arguments)}"
            }
            .WithCustomEnvironment(environmentUnderTest)
            .WithCustomScrubbers(
               ScrubbersDefinition.Empty
               .AddScrubber(sb => sb.Replace(DateTime.Now.ToString("MM/dd/yyyy"), "**/**/****")));

            VerificationEngine engine = new VerificationEngine(_log);
            await engine.Execute(options)
                .ConfigureAwait(false);
        }

        private string GetScenarioName(string[]? args)
        {
            StringBuilder sb = new StringBuilder();

            if (args != null)
            {
                sb.Append('.');

                for (int index = 0; index < args.Length; index += 2)
                {
                    sb.Append($"{args[index].Replace("--", "")}={args[index + 1]}.");
                }
            }

            return sb.ToString(0, Math.Max(0, sb.Length - 1));
        }
    }
}
