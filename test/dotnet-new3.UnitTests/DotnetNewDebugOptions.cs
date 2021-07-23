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
    public class DotnetNewDebugOptions
    {
        private const string DotnetNewOutput =
@"The 'dotnet new3' command creates a .NET project based on a template.

Common templates are:
Template Name        Short Name  Language    Tags          
-------------------  ----------  ----------  --------------
Class Library        classlib    [C#],F#,VB  Common/Library
Console Application  console     [C#],F#,VB  Common/Console

An example would be:
   dotnet new3 console

Display template options with:
   dotnet new3 console -h
Display all installed templates with:
   dotnet new3 --list
Display templates available on NuGet.org with:
   dotnet new3 web --search";

        private readonly ITestOutputHelper _log;

        public DotnetNewDebugOptions(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanShowBasicInfoWithDebugReinit()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--debug:reinit")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(DotnetNewOutput);
        }

        [Fact]
        public void CanShowBasicInfoWithDebugRebuildCache()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "--debug:rebuildcache")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut(DotnetNewOutput);
        }
    }
}
