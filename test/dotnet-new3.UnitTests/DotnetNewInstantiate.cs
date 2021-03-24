using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.Edge.Template;
using System.IO;
using Microsoft.TemplateEngine.TestHelper;

namespace dotnet_new3.UnitTests
{
    public class DotnetNewInstantiate
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewInstantiate(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanInstantiateTemplate()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.");
        }

        [Fact]
        public void CannotInstantiateUnknownTemplate()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "webapp", "--quiet")
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("No templates found matching: 'webapp'.")
                .And.HaveStdErrContaining("To list installed templates, run 'dotnet new3 --list'.")
                .And.HaveStdErrContaining("To search for the templates on NuGet.org, run 'dotnet new3 webapp --search'.");
        }

        [Fact]
        public void CanInstantiateTemplateWithSingleNonDefaultLanguageChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "basic")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Basic FSharp\" was created successfully.");
        }

        [Fact]
        public void CannotInstantiateTemplateWhenAmbiguousLanguageChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "basic")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template to instantiate, these templates matched your input:")
                .And.HaveStdErrContaining("Re-run the command specifying the language to use with --language option.")
                .And.HaveStdErrContaining("basic").And.HaveStdErrContaining("F#").And.HaveStdErrContaining("VB");
        }

        [Fact]
        public void CannotInstantiateTemplateWhenAmbiguousGroupChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "conf", "--quiet")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template to instantiate, these templates matched your input:")
                .And.HaveStdErrContaining("Re-run the command using the template's exact short name.")
                .And.HaveStdErrContaining("webconfig").And.HaveStdErrContaining("nugetconfig").And.NotHaveStdErrContaining("classlib");

            new DotnetNewCommand(_log, "file")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template to instantiate, these templates matched your input:")
                .And.HaveStdErrContaining("Re-run the command using the template's exact short name.")
                .And.HaveStdErrContaining("tool-manifest").And.HaveStdErrContaining("sln").And.NotHaveStdErrContaining("console");
        }

        [Fact]
        public void CannotInstantiateTemplateWhenParameterIsInvalid()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--fake", "--quiet")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   '--fake' is not a valid option")
                .And.HaveStdErrContaining("For more information, run 'dotnet new3 console --help'.");

            new DotnetNewCommand(_log, "console", "--framework", "fake")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   'fake' is not a valid value for --framework. The possible values are:")
                .And.HaveStdErrContaining("      net5.0          - Target net5.0")
                .And.HaveStdErrContaining("      netcoreapp3.1   - Target netcoreapp3.1")
                .And.HaveStdErrContaining("For more information, run 'dotnet new3 console --help'.");

            new DotnetNewCommand(_log, "console", "--framework", "netcoreapp")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   The value 'netcoreapp' is ambiguous for option --framework. The possible values are:")
                .And.HaveStdErrContaining("      netcoreapp2.1   - Target netcoreapp2.1")
                .And.HaveStdErrContaining("      netcoreapp3.1   - Target netcoreapp3.1")
                .And.HaveStdErrContaining("For more information, run 'dotnet new3 console --help'.");

            new DotnetNewCommand(_log, "console", "--framework", "netcoreapp", "--fake")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   The value 'netcoreapp' is ambiguous for option --framework. The possible values are:")
                .And.HaveStdErrContaining("      netcoreapp2.1   - Target netcoreapp2.1")
                .And.HaveStdErrContaining("      netcoreapp3.1   - Target netcoreapp3.1")
                .And.HaveStdErrContaining("   '--fake' is not a valid option")
                .And.HaveStdErrContaining("For more information, run 'dotnet new3 console --help'.");
        }

        [Fact]
        public void CannotInstantiateTemplateWhenPrecedenceIsSame()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate1", _log, workingDirectory, home);
            Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate2", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "basic")
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template to instantiate, the following installed templates are conflicting:")
                .And.HaveStdErrContaining("Uninstall the templates or the packages to keep only one template from the list.")
                .And.HaveStdErrContaining("TestAssets.SamePrecedenceGroup.BasicTemplate2")
                .And.HaveStdErrContaining("TestAssets.SamePrecedenceGroup.BasicTemplate1")
                .And.HaveStdErrContaining("basic")
                .And.HaveStdErrContaining("C#")
                .And.HaveStdErrContaining("Test Asset")
                .And.HaveStdErrContaining("100")
                .And.HaveStdErrContaining($"{Path.DirectorySeparatorChar}test_templates{Path.DirectorySeparatorChar}TemplateResolution{Path.DirectorySeparatorChar}SamePrecedenceGroup{Path.DirectorySeparatorChar}BasicTemplate2")
                .And.HaveStdErrContaining($"{Path.DirectorySeparatorChar}test_templates{Path.DirectorySeparatorChar}TemplateResolution{Path.DirectorySeparatorChar}SamePrecedenceGroup{Path.DirectorySeparatorChar}BasicTemplate1");
        }
    }
}
