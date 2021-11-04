// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewHelp : IClassFixture<SharedHomeDirectory>
    {
        #region HelpConstants
        private const string HelpOutput =
@"Usage: new3 [options]

Options:
  -h, --help                     Displays help for this command.
  -l, --list <PARTIAL_NAME>      Lists templates containing the specified template name. If no name is specified, lists all templates.
  -n, --name                     The name for the output being created. If no name is specified, the name of the output directory is used.
  -o, --output                   Location to place the generated output.
  -i, --install                  Installs a source or a template package.
  -u, --uninstall                Uninstalls a source or a template package.
  --interactive                  Allows the internal dotnet restore command to stop and wait for user input or action (for example to complete authentication).
  --add-source, --nuget-source   Specifies a NuGet source to use during install.
  --type                         Filters templates based on available types. Predefined values are ""project"" and ""item"".
  --dry-run                      Displays a summary of what would happen if the given command line were run if it would result in a template creation.
  --force                        Forces content to be generated even if it would change existing files.
  -lang, --language              Filters templates based on language and specifies the language of the template to create.
  --update-check                 Check the currently installed template packages for updates.
  --update-apply                 Check the currently installed template packages for update, and install the updates.
  --search <PARTIAL_NAME>        Searches for the templates on NuGet.org.
  --author <AUTHOR>              Filters the templates based on the template author. Applicable only with --search or --list | -l option.
  --package <PACKAGE>            Filters the templates based on NuGet package ID. Applies to --search.
  --columns <COLUMNS_LIST>       Comma separated list of columns to display in --list and --search output.
                                 The supported columns are: language, tags, author, type.
  --columns-all                  Display all columns in --list and --search output.
  --tag <TAG>                    Filters the templates based on the tag. Applies to --search and --list.
  --no-update-check              Disables checking for the template package updates when instantiating a template.";

        private const string ConsoleHelp =
@"Console App (C#)
Author: Microsoft
Description: A project for creating a command-line application that can run on .NET Core on Windows, Linux and macOS
Options:                                                                             
  -f|--framework  The target framework for the project.                              
                      net7.0           - Target net7.0                               
                      net5.0           - Target net5.0                               
                      netcoreapp3.1    - Target netcoreapp3.1                        
                  Default: net7.0                                                    

  --langVersion   Sets the LangVersion property in the created project file          
                  text - Optional                                                    

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional                                                    
                  Default: false                                                     


To see help for other template languages (F#, VB), use --language option:
   dotnet new3 console -h --language F#";

        private const string ClassLibHelp =
@"Class Library (C#)
Author: Microsoft
Description: A project for creating a class library that targets .NET Standard or .NET Core
Options:                                                                             
  -f|--framework  The target framework for the project.                              
                      net7.0            - Target net7.0                              
                      netstandard2.1    - Target netstandard2.1                      
                      netstandard2.0    - Target netstandard2.0                      
                      net5.0            - Target net5.0                              
                      netcoreapp3.1     - Target netcoreapp3.1                       
                  Default: net7.0                                                    

  --langVersion   Sets the LangVersion property in the created project file          
                  text - Optional                                                    

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional                                                    
                  Default: false                                                     


To see help for other template languages (F#, VB), use --language option:
   dotnet new3 classlib -h --language F#";

        #endregion
        private readonly ITestOutputHelper _log;
        private readonly SharedHomeDirectory _fixture;

        public DotnetNewHelp(SharedHomeDirectory fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Fact]
        public void CanShowHelp()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(HelpOutput);

            new DotnetNewCommand(_log, "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(HelpOutput);
        }

        [Fact]
        public void CanShowHelpForTemplate()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(ConsoleHelp)
                .And.NotHaveStdOutContaining(HelpOutput);

            new DotnetNewCommand(_log, "classlib", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(ClassLibHelp)
                .And.NotHaveStdOutContaining(HelpOutput);
        }

        [Fact]
        public void CannotShowHelpForTemplate_PartialNameMatch()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "class", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErr(
@"No templates found matching: 'class'.

To list installed templates, run:
   dotnet new3 --list
To search for the templates on NuGet.org, run:
   dotnet new3 class --search");
        }

        [Fact]
        public void CannotShowHelpForTemplate_FullNameMatch()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "Console App", "-h")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErr(
@"No templates found matching: 'Console App'.

To list installed templates, run:
   dotnet new3 --list
To search for the templates on NuGet.org, run:
   dotnet new3 'Console App' --search");
        }

        [Fact]
        public void CannotShowHelpForTemplate_WhenAmbiguousLanguageChoice()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, _fixture.HomeDirectory);
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, workingDirectory, _fixture.HomeDirectory);

            new DotnetNewCommand(_log, "basic", "--help")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template, these templates matched your input:")
                .And.HaveStdErrContaining("Re-run the command specifying the language to use with --language option.")
                .And.HaveStdErrContaining("basic").And.HaveStdErrContaining("F#").And.HaveStdErrContaining("VB");
        }

        [Fact]
        public void CanShowHelpForTemplate_MatchOnChoice()
        {
            const string ConsoleHelp =
@"Console App (C#)
Author: Microsoft
Description: A project for creating a command-line application that can run on .NET Core on Windows, Linux and macOS
Options:                                                                            
  --langVersion  Sets the LangVersion property in the created project file          
                 text - Optional                                                    

  --no-restore   If specified, skips the automatic restore of the project on create.
                 bool - Optional                                                    
                 Default: false                                                     


To see help for other template languages (F#, VB), use --language option:
   dotnet new3 console -h --language F#";

            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help", "--framework", "net7.0")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOut(ConsoleHelp)
                .And.NotHaveStdOutContaining(HelpOutput);
        }

        [Fact]
        public void CannotShowHelpForTemplate_MatchOnChoiceWithoutValue()
        {
            string expectedOutput =
@"Error: Invalid option(s):
--framework 
   '' is not a valid value for --framework. The possible values are:
      net5.0          - Target net5.0
      net7.0          - Target net7.0
      netcoreapp3.1   - Target netcoreapp3.1

For more information, run:
   dotnet new3 console -h";
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help", "--framework")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErr(expectedOutput);
        }

        [Fact]
        public void CannotShowHelpForTemplate_MatchOnUnexistingParam()
        {
            string expectedOutput =
@"Error: Invalid option(s):
--do-not-exist
   '--do-not-exist' is not a valid option

For more information, run:
   dotnet new3 console -h";

            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help", "--do-not-exist")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErr(expectedOutput);
        }

        [Fact]
        public void CanShowHelpForTemplate_MatchOnNonChoiceParam()
        {
            const string ConsoleHelp =
@"Console App (C#)
Author: Microsoft
Description: A project for creating a command-line application that can run on .NET Core on Windows, Linux and macOS
Options:                                                                             
  -f|--framework  The target framework for the project.                              
                      net7.0           - Target net7.0                               
                      net5.0           - Target net5.0                               
                      netcoreapp3.1    - Target netcoreapp3.1                        
                  Default: net7.0                                                    

  --langVersion   Sets the LangVersion property in the created project file          
                  text - Optional                                                    
                  Configured Value: 8.0                                              

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional                                                    
                  Default: false                                                     


To see help for other template languages (F#, VB), use --language option:
   dotnet new3 console -h --language F#";

        string workingDirectory = TestUtils.CreateTemporaryFolder();

        new DotnetNewCommand(_log, "console", "--help", "--langVersion", "8.0")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOut(ConsoleHelp)
                .And.NotHaveStdOutContaining(HelpOutput);
        }

        [Fact]
        public void CanShowHelpForTemplate_MatchOnLanguage()
        {
            const string ConsoleHelp =
@"Console App (F#)
Author: Microsoft
Description: A project for creating a command-line application that can run on .NET Core on Windows, Linux and macOS
Options:                                                                             
  -f|--framework  The target framework for the project.                              
                      net7.0           - Target net7.0                               
                      net5.0           - Target net5.0                               
                      netcoreapp3.1    - Target netcoreapp3.1                        
                  Default: net7.0                                                    

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional                                                    
                  Default: false                                                     


To see help for other template languages (C#, VB), use --language option:
   dotnet new3 console -h --language C#";

            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help", "--language", "F#")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute()
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOut(ConsoleHelp)
                    .And.NotHaveStdOutContaining(HelpOutput);
        }

        [Fact]
        public void WontShowLanguageHintInCaseOfOneLang()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "globaljson", "--help")
                    .WithCustomHive(_fixture.HomeDirectory)
                    .WithWorkingDirectory(workingDirectory)
                    .Execute()
                    .Should().Pass()
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("To see help for other template languages");
        }

        [Fact]
        public void CannotShowHelpForTemplate_MatchOnNonChoiceParamWithoutValue()
        {
            string expectedOutput =
@"Error: Invalid option(s):
--langVersion 
   '' is not a valid value for --langVersion.";

            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help", "--langVersion")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErr(expectedOutput);
        }
    }
}
