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
    public class DotnetNewHelp
    {
        #region HelpConstants
        private const string HelpOutput =
@"Usage: new3 [options]

Options:
  -h, --help                     Displays help for this command.
  -l, --list                     Lists templates containing the specified template name. If no name is specified, lists all templates.
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
  --search                       Searches for the templates on NuGet.org.
  --author <AUTHOR>              Filters the templates based on the template author. Applicable only with --search or --list | -l option.
  --package <PACKAGE>            Filters the templates based on NuGet package ID. Applies to --search.
  --columns <COLUMNS_LIST>       Comma separated list of columns to display in --list and --search output.
                                 The supported columns are: language, tags, author, type.
  --columns-all                  Display all columns in --list and --search output.
  --tag <TAG>                    Filters the templates based on the tag. Applies to --search and --list.
  --no-update-check              Disables checking for the template package updates when instantiating a template.";

        private const string ConsoleHelp =
@"Console Application (C#)
Author: Microsoft
Description: A project for creating a command-line application that can run on .NET Core on Windows, Linux and macOS
Options:                                                                             
  -f|--framework  The target framework for the project.                              
                      net6.0           - Target net6.0                               
                      net5.0           - Target net5.0                               
                      netcoreapp3.1    - Target netcoreapp3.1                        
                      netcoreapp3.0    - Target netcoreapp3.0                        
                      netcoreapp2.2    - Target netcoreapp2.2                        
                      netcoreapp2.1    - Target netcoreapp2.1                        
                      netcoreapp2.0    - Target netcoreapp2.0                        
                      netcoreapp1.0    - Target netcoreapp1.0                        
                      netcoreapp1.1    - Target netcoreapp1.1                        
                  Default: net6.0                                                    

  --langVersion   Sets the LangVersion property in the created project file          
                  text - Optional                                                    

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional                                                    
                  Default: false                                                     ";

        private const string ClassLibHelp =
@"Class Library (C#)
Author: Microsoft
Description: A project for creating a class library that targets .NET Standard or .NET Core
Options:                                                                             
  -f|--framework  The target framework for the project.                              
                      net6.0            - Target net6.0                              
                      netstandard2.1    - Target netstandard2.1                      
                      netstandard2.0    - Target netstandard2.0                      
                      net5.0            - Target net5.0                              
                      netcoreapp3.1     - Target netcoreapp3.1                       
                      netcoreapp3.0     - Target netcoreapp3.0                       
                      netcoreapp2.2     - Target netcoreapp2.2                       
                      netcoreapp2.1     - Target netcoreapp2.1                       
                      netcoreapp2.0     - Target netcoreapp2.0                       
                      netcoreapp1.0     - Target netcoreapp1.0                       
                      netcoreapp1.1     - Target netcoreapp1.1                       
                      netstandard1.0    - Target netstandard1.0                      
                      netstandard1.1    - Target netstandard1.1                      
                      netstandard1.2    - Target netstandard1.2                      
                      netstandard1.3    - Target netstandard1.3                      
                      netstandard1.4    - Target netstandard1.4                      
                      netstandard1.5    - Target netstandard1.5                      
                      netstandard1.6    - Target netstandard1.6                      
                  Default: net6.0                                                    

  --langVersion   Sets the LangVersion property in the created project file          
                  text - Optional                                                    

  --no-restore    If specified, skips the automatic restore of the project on create.
                  bool - Optional                                                    
                  Default: false                                                     

  --nullable      Whether to enable nullable reference types for this project.       
                  bool - Optional                                                    
                  Default: true                                                      ";

        #endregion
        private readonly ITestOutputHelper _log;

        public DotnetNewHelp(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanShowBasicInfo()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--quiet")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console Application\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.HaveStdOutContaining("Examples:")
                .And.HaveStdOutMatching("dotnet new3 [A-Za-z\\-]+")
                .And.HaveStdOutContaining("dotnet new3 --help")
                .And.HaveStdOutMatching("dotnet new3 [A-Za-z\\-]+ --help")
                .And.NotHaveStdOutContaining(HelpOutput);
        }

        [Fact]
        public void CanShowHelp()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--help", "--quiet")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(HelpOutput);

            new DotnetNewCommand(_log, "-h")
                .WithCustomHive(home)
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--help", "--quiet")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(ConsoleHelp)
                .And.NotHaveStdOutContaining(HelpOutput);

            new DotnetNewCommand(_log, "classlib", "-h")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(ClassLibHelp)
                .And.NotHaveStdOutContaining(HelpOutput);
        }
    }
}
