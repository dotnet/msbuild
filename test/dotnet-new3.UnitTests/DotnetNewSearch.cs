// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.IntegrationTests
{
    public class DotnetNewSearch : IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ITestOutputHelper _log;

        public DotnetNewSearch(SharedHomeDirectory sharedHome, ITestOutputHelper log)
        {
            _sharedHome = sharedHome;
            _log = log;
        }

        [Fact]
        public void BasicTest()
        {
            new DotnetNewCommand(_log, "console", "--search")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");
        }

        [Fact]
        public void CanShowTags()
        {
            new DotnetNewCommand(_log, "console", "--search", "--columns", "tags")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Tags\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");
        }

        [Fact]
        public void CanShowAllColumns()
        {
            new DotnetNewCommand(_log, "console", "--search", "--columns-all")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Type\\s+Tags\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");
        }

        [Fact]
        public void CanFilterTags()
        {
            new DotnetNewCommand(_log, "console", "--search", "--columns", "tags", "--tag", "Common")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Tags\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            //TODO: implement check for filtering
        }
    }
}
