// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewListTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ITestOutputHelper _log;

        public DotnetNewListTests(SharedHomeDirectory sharedHome, ITestOutputHelper log) : base(log)
        {
            _sharedHome = sharedHome;
            _sharedHome.InstallPackage("Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0");
            _log = log;
        }

        [Theory]
        [InlineData("--list c")]
        [InlineData("-l c")]
        [InlineData("list c")]
        [InlineData("c --list")]
        public void BasicTest_WithNameCriteria(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"These templates matched your input: 'c'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [Theory]
        [InlineData("--list --columns-all")]
        [InlineData("--columns-all --list")]
        [InlineData("list --columns-all")]
        public void CanShowAllColumns(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Type\\s+Author\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+project\\s+Microsoft\\s+Common/Console");
        }

        [Theory]
        [InlineData("--list --tag Common")]
        [InlineData("-l --tag Common")]
        [InlineData("list --tag Common")]
        [InlineData("--tag Common --list")]
        public void CanFilterTags(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"These templates matched your input: --tag='Common'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [Theory]
        [InlineData("app --list --tag Common")]
        [InlineData("app -l --tag Common")]
        [InlineData("--list app --tag Common")]
        [InlineData("list app --tag Common")]
        public void CanFilterTags_WithNameCriteria(string command)
        {
            new DotnetNewCommand(_log, command.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"These templates matched your input: 'app', --tag='Common'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.NotHaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [Fact]
        public void CanShowMultipleShortNames()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--install", "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                  .WithCustomHive(home)
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");

            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");

            new DotnetNewCommand(_log, "webapp", "--list")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'webapp'")
                .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");

            new DotnetNewCommand(_log, "razor", "--list")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'razor'")
                .And.HaveStdOutMatching("ASP\\.NET Core Web App\\s+webapp,razor\\s+\\[C#\\]\\s+Web/MVC/Razor Pages");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--framework")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "-f")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: 'c', -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--framework")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "-f")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByNonChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void IgnoresValueForNonChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--no-restore", "invalid")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --no-restore")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--no-restore", "invalid")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --no-restore")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByChoiceParameterWithValue()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "--framework", "net5.0")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: 'c', --framework='net5.0'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "c", "--list", "-f", "net5.0")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: 'c', -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--framework", "net5.0")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input: --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "-f", "net5.0")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should()
              .ExitWith(0)
              .And.NotHaveStdErr()
              .And.HaveStdOutContaining("These templates matched your input: -f")
              .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
              .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
              .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
              .And.NotHaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotListTemplatesWithUnknownParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: --unknown.")
                .And.HaveStdErrContaining("9 template(s) partially matched, but failed on --unknown.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new <TEMPLATE_NAME> --search");

            new DotnetNewCommand(_log, "c", "--list", "--unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'c', --unknown.")
                .And.HaveStdErrContaining("6 template(s) partially matched, but failed on --unknown.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");

            new DotnetNewCommand(_log, "c", "--list", "--unknown", "--language", "C#")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should().Fail()
              .And.HaveStdErrContaining("No templates found matching: 'c', language='C#', --unknown.")
              .And.HaveStdErrContaining("6 template(s) partially matched, but failed on language='C#', --unknown.")
              .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotListTemplatesWithUnknownValueForChoiceParameter()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: --framework='unknown'.")
                .And.HaveStdErrContaining("9 template(s) partially matched, but failed on --framework='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new <TEMPLATE_NAME> --search");

            new DotnetNewCommand(_log, "c", "--list", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'c', --framework='unknown'.")
                .And.HaveStdErrContaining("6 template(s) partially matched, but failed on --framework='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotListTemplatesForInvalidFilters()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutMatching("NuGet Config\\s+nugetconfig\\s+Config");

            new DotnetNewCommand(_log, "--list", "--language", "unknown", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: language='unknown'.")
                .And.HaveStdErrContaining("9 template(s) partially matched, but failed on language='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new <TEMPLATE_NAME> --search");

            new DotnetNewCommand(_log, "c", "--list", "--language", "unknown", "--framework", "unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'c', language='unknown'.")
                .And.HaveStdErrContaining("6 template(s) partially matched, but failed on language='unknown'.")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new c --search");
        }

        [Fact]
        public void TemplateGroupingTest()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            InstallTestTemplate("TemplateGrouping", _log, home, workingDir);

            new DotnetNewCommand(_log, "--list", "--columns-all")
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching("Basic FSharp +template-grouping +\\[C#],F# +item +Author1 +Test Asset +\\r?\\n +Q# +item,project +Author2 +Test Asset");
        }

        [Theory]
        [InlineData("author", "Author", "Microsoft")]
        [InlineData("type", "Type", "")]
        [InlineData("tags", "Tags", "Solution")]
        [InlineData("language", "Language", "")]
        public void TemplateWithSpecifiedColumnOutput(string columnName, string columnHeader, string columnValue)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--install", "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                  .WithCustomHive(home)
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0);

            new DotnetNewCommand(_log, "list", "--columns", columnName)
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("These templates matched your input:")
                .And.HaveStdOutMatching($"Template Name\\s+Short Name\\s+{columnHeader}")
                .And.HaveStdOutMatching($"Solution File\\s+sln,solution\\s+{columnValue}");
        }

        [Theory]
        [InlineData("c --list", "--list c")]
        [InlineData("c --list --language F#", "--list c --language F#")]
        [InlineData("c --list --columns-all", "--list c --columns-all")]
        public void CanFallbackToListOption(string command1, string command2)
        {
            CommandResult commandResult1 = new DotnetNewCommand(_log, command1.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            CommandResult commandResult2 = new DotnetNewCommand(_log, command2.Split())
               .WithCustomHive(_sharedHome.HomeDirectory)
               .Execute();

            Assert.Equal(commandResult1.StdOut, commandResult2.StdOut);
        }

        [Theory]
        [InlineData("--list foo --columns-all bar", "bar", "foo")]
        [InlineData("list foo --columns-all bar", "bar", "foo")]
        [InlineData("-l foo --columns-all bar", "bar", "foo")]
        [InlineData("--list foo bar", "bar", "foo")]
        [InlineData("list foo bar", "bar", "foo")]
        [InlineData("foo --list bar", "foo", "bar")]
        [InlineData("foo list bar", "foo", "bar")]
        [InlineData("foo --list bar --language F#", "foo", "bar")]
        [InlineData("foo --list --columns-all bar", "foo", "bar")]
        [InlineData("foo --list --columns-all --framework net6.0 bar", "bar|net6.0|foo", "--framework")]
        [InlineData("foo --list --columns-all -other-param --framework net6.0 bar", "bar|--framework|net6.0|foo", "-other-param")]
        public void CannotShowListOnParseError(string command, string invalidArguments, string validArguments)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, command.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            commandResult.Should().Fail();
            foreach (string arg in invalidArguments.Split('|'))
            {
                commandResult.Should().HaveStdErrMatching($"Unrecognized command or (argument\\(s\\)\\:|argument) '{arg}'");
            }

            foreach (string arg in validArguments.Split('|'))
            {
                commandResult.Should()
                    .NotHaveStdErrContaining($"Unrecognized command or argument '{arg}'")
                    .And.NotHaveStdErrContaining($"Unrecognized command or argument(s): '{arg}'");
            }
        }
    }
}
