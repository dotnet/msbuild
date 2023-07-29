// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishTrimmedWindowsFormsAndWPFApps : SdkTest
    {
        public GivenThatWeWantToPublishTrimmedWindowsFormsAndWPFApps(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_windows_Forms_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsBuildErrorFailTest",
                TargetFrameworks = targetFramework,
                IsWinExe=true
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1175");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_windows_Forms_app_with_error_suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsBuildErrorSuppressPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["_SuppressWinFormsTrimError"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1175 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWindowsFormsIsNotSupported);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_windows_Forms_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsErrorPresentFailTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1175");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_windows_Forms_app_with_error_suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WinformsErrorSuppressedPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWindowsForms"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["_SuppressWinFormsTrimError"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1175 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWindowsFormsIsNotSupported);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_wpf_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WpfErrorPresentFailTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1168");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_builds_wpf_app_with_error_suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WpfErrorSuppressedPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["_SuppressWpfTrimError"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1168 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWpfIsNotSupported);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_wpf_app_with_error()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WpfErrorPresentPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1168");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_wpf_app_with_error_Suppressed()
        {
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            TestProject testProject = new TestProject()
            {
                Name = "WpfPassTest",
                TargetFrameworks = targetFramework,
                IsWinExe = true,
                SelfContained = "true"
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x64";
            testProject.AdditionalProperties["_SuppressWpfTrimError"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass()
                .And
                //cannot check for absence of NETSDK1168 since that is used with /nowarn: in some configurations
                .NotHaveStdOutContaining(Strings.@TrimmingWpfIsNotSupported);
        }
    }
}
