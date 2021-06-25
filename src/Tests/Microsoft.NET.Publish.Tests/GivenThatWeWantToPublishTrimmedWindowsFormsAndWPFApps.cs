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
        public void It_publishes_windows_Forms_app_with_warning()
        {
            var targetFramework = "net6.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsWarnPresentPassTest",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1175");
        }

        [WindowsOnlyFact]
        public void It_publishes_windows_Forms_app_with_warning_suppressed()
        {
            var targetFramework = "net6.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsPassTest",
                TargetFrameworks = targetFramework
            };
            testProject.IsWinExe = true;
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["NoWarn"] = "NETSDK1175";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1175 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWindowsFormsIsNotSupported);
        }

        [WindowsOnlyFact]
        public void It_publishes_wpf_app_with_warning()
        {
            var targetFramework = "net6.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WpfWarnPresentPassTest",
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
                .HaveStdOutContaining("NETSDK1168");
        }

        [WindowsOnlyFact]
        public void It_publishes_wpf_app_with_warning_Suppressed()
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
            testProject.AdditionalProperties["NoWarn"] = "NETSDK1168";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";            
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1168 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWpfIsNotSupported);
        }
    }
}
