// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishTrimmedWindowsFormsAndWPFApps : SdkTest
    {
        public GivenThatWeWantToPublishTrimmedWindowsFormsAndWPFApps(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_errors_on_publishing_winforms_app()
        {
            var targetFramework = "net6.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsFailTest",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1164");
        }

        [WindowsOnlyFact]
        public void It_publishes_windows_forms_app_with_com_support()
        {
            var targetFramework = "net6.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsPassTest",
                TargetFrameworks = targetFramework
            };
            testProject.IsWinExe = true;
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["BuiltInComSupport"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass();

        }

        [WindowsOnlyFact]
        public void It_publishes_wpf_app_with_warning()
        {
            var targetFramework = "net6.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WpfPassTest",
                TargetFrameworks = targetFramework
            };
            testProject.IsWinExe = true;
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1165");
        }
    }
}
