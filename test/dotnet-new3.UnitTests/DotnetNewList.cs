// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewList : IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ITestOutputHelper _log;

        public DotnetNewList(SharedHomeDirectory sharedHome, ITestOutputHelper log)
        {
            _sharedHome = sharedHome;
            _log = log;
        }

        [Fact]
        public void BasicTest()
        {
            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console Application\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [Fact]
        public void CanShowAllColumns()
        {
            new DotnetNewCommand(_log, "--list", "--columns-all")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Type\\s+Author\\s+Tags")
                .And.HaveStdOutMatching("Console Application\\s+console\\s+\\[C#\\],F#,VB\\s+project\\s+Microsoft\\s+Common/Console");
        }

        [Fact]
        public void CanFilterTags()
        {
            new DotnetNewCommand(_log, "--list", "--tag", "Common")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console Application\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library");
        }

        [Fact]
        public void CanSortByName()
        {
            const string expectedOutput =
@"Template Name                                 Short Name     Language    Tags                  
--------------------------------------------  -------------  ----------  ----------------------
ASP.NET Core Empty                            web            [C#],F#     Web/Empty             
ASP.NET Core Web API                          webapi         [C#],F#     Web/WebAPI            
ASP.NET Core Web App                          webapp         [C#]        Web/MVC/Razor Pages   
ASP.NET Core Web App (Model-View-Controller)  mvc            [C#],F#     Web/MVC               
ASP.NET Core gRPC Service                     grpc           [C#]        Web/gRPC              
Blazor Server App                             blazorserver   [C#]        Web/Blazor            
Blazor WebAssembly App                        blazorwasm     [C#]        Web/Blazor/WebAssembly
Class Library                                 classlib       [C#],F#,VB  Common/Library        
Console Application                           console        [C#],F#,VB  Common/Console        
Dotnet local tool manifest file               tool-manifest              Config                
NuGet Config                                  nugetconfig                Config                
Razor Class Library                           razorclasslib  [C#]        Web/Razor/Library     
Solution File                                 sln                        Solution              
Web Config                                    webconfig                  Config                
Worker Service                                worker         [C#],F#     Common/Worker/Web     
dotnet gitignore file                         gitignore                  Config                
global.json file                              globaljson                 Config                ";

            string home = TestUtils.CreateTemporaryFolder();
            Helpers.InstallNuGetTemplate("Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0", _log, null, home);

            new DotnetNewCommand(_log, "--list")
                .WithCustomHive(home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOut(expectedOutput)
                .And.NotHaveStdErr();

        }
    }
}
