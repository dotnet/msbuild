// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibraryWithOSMinimumVersion : SdkTest
    {
        public GivenThatWeWantToBuildALibraryWithOSMinimumVersion(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenPropertiesAreNotSetItShouldNotGenerateSupportedOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute(null))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute(null));
        }

        [Fact]
        public void WhenPropertiesAreSetItCanGenerateSupportedOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "fakeOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["TargetPlatformSupported"] = "true";
            testProject.AdditionalProperties["TargetPlatformVersionSupported"] = "true";
            testProject.AdditionalProperties["SupportedOSPlatformVersion"] = "13.2";
            testProject.AdditionalProperties["TargetPlatformVersion"] = "14.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("fakeOS14.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("fakeOS13.2"));
        }

        [Fact]
        public void WhenSupportedOSPlatformVersionIsNotSetTargetPlatformVersionIsSetItCanGenerateSupportedOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "fakeOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["TargetPlatformSupported"] = "true";
            testProject.AdditionalProperties["TargetPlatformVersionSupported"] = "true";
            testProject.AdditionalProperties["TargetPlatformVersion"] = "13.2";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("fakeOS13.2"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("fakeOS13.2"));
        }

        [Fact]
        public void WhenUsingDefaultTargetPlatformVersionItCanGenerateSupportedOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject();
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "windows";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("Windows7.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("Windows7.0"));
        }

        [WindowsOnlyTheory]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows", "Windows7.0")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041", "Windows10.0.19041.0")]
        public void WhenUsingTargetPlatformInTargetFrameworkItCanGenerateSupportedOSPlatformAttribute(string targetFramework, string expectedAttribute)
        {
            TestProject testProject = SetUpProject(targetFramework);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute(expectedAttribute))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute(expectedAttribute));
        }

        [Fact]
        public void WhenUsingZeroedSupportedOSPlatformVersionItCanGenerateSupportedOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject($"{ToolsetInfo.CurrentTargetFramework}-windows");
            testProject.AdditionalProperties["SupportedOSPlatformVersion"] = "0.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("Windows7.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("Windows"));
        }

        [Fact]
        public void WhenSupportedOSPlatformVersionIsHigherThanTargetPlatformVersionItShouldError()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "fakeOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["TargetPlatformVersionSupported"] = "true";
            testProject.AdditionalProperties["TargetPlatformVersion"] = "13.2";
            testProject.AdditionalProperties["SupportedOSPlatformVersion"] = "14.0";
            testProject.AdditionalProperties["TargetPlatformSupported"] = "true";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new DotnetBuildCommand(Log, Path.Combine(testAsset.Path, "Project", "Project.csproj"));
            buildCommand.Execute()
                .Should()
                .Fail().And.HaveStdOutContaining("NETSDK1135");
        }

        [WindowsOnlyFact]
        public void WhenTargetPlatformMinVersionIsSetForWindowsItIsUsedForTheSupportedOSPlatformAttribute()
        {
            TestProject testProject = SetUpProject($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041");
            testProject.AdditionalProperties["TargetPlatformMinVersion"] = "10.0.18362.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("Windows10.0.19041.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("Windows10.0.18362.0"));
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void WhenTargetingWindowsSupportedOSVersionPropertySetsTargetPlatformMinVersion()
        {
            TestProject testProject = SetUpProject($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041");
            testProject.AdditionalProperties["SupportedOSPlatformVersion"] = "10.0.18362.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("Windows10.0.19041.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("Windows10.0.18362.0"));

            var getValuesCommand = new GetValuesCommand(testAsset, "TargetPlatformMinVersion");
            getValuesCommand.Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValues()
                .Should()
                .BeEquivalentTo("10.0.18362.0");
        }

        [WindowsOnlyFact]
        public void WhenTargetingWindowsSupportedOSPlatformVersionPropertyIsPreferredOverTargetPlatformMinVersion()
        {
            TestProject testProject = SetUpProject($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041");
            testProject.AdditionalProperties["TargetPlatformMinVersion"] = "10.0.18362.0";
            testProject.AdditionalProperties["SupportedOSPlatformVersion"] = "10.0.17663.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("Windows10.0.19041.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("Windows10.0.17663.0"));

        }

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("net48")]
        public void WhenNotTargetingNet5TargetPlatformMinVersionPropertyCanBeSet(string targetFramework)
        {
            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = true,
                TargetFrameworks = targetFramework,
            };

            testProject.AdditionalProperties["TargetPlatformMinVersion"] = "10.0.18362.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void WhenNotTargetingWindowsTargetPlatformMinVersionPropertyIsIgnored()
        {
            TestProject testProject = SetUpProject();

            var targetPlatformIdentifier = "fakeOS";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatformIdentifier;
            testProject.AdditionalProperties["TargetPlatformSupported"] = "true";
            testProject.AdditionalProperties["TargetPlatformVersionSupported"] = "true";
            testProject.AdditionalProperties["SupportedOSPlatformVersion"] = "13.2";
            testProject.AdditionalProperties["TargetPlatformVersion"] = "14.0";
            testProject.AdditionalProperties["TargetPlatformMinVersion"] = "12.0";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var runCommand = new DotnetCommand(Log, "run")
            {
                WorkingDirectory = Path.Combine(testAsset.TestRoot, testProject.Name)
            };
            runCommand.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(TargetPlatformAttribute("fakeOS14.0"))
                .And.HaveStdOutContaining(SupportedOSPlatformAttribute("fakeOS13.2"));
        }

        private static string TargetPlatformAttribute(string targetPlatform)
        {
            string expected = string.IsNullOrEmpty(targetPlatform) ?
                "NO TargetPlatformAttribute" :
                $"TargetPlatform:'{targetPlatform}'";

            return expected;
        }

        private static string SupportedOSPlatformAttribute(string supportedOSPlatform)
        {
            string expected = string.IsNullOrEmpty(supportedOSPlatform) ?
                "NO SupportedOSPlatformAttribute" :
                $"SupportedOSPlatform:'{supportedOSPlatform}'";

            return expected;
        }

        private static TestProject SetUpProject(string targetFramework = ToolsetInfo.CurrentTargetFramework)
        {
            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = true,
                TargetFrameworks = targetFramework,
            };

            testProject.SourceFiles["PrintAttributes.cs"] = _printAttributes;
            return testProject;
        }

        private static readonly string _printAttributes = @"
using System;
using System.Runtime.Versioning;

namespace CustomAttributesTestApp
{
    internal static class CustomAttributesTestApp
    {
        public static void Main()
        {
            var assembly = typeof(CustomAttributesTestApp).Assembly;

            object[] targetPlatformAttributes = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetPlatformAttribute), false);
            if (targetPlatformAttributes.Length > 0)
            {
                var attribute = targetPlatformAttributes[0] as System.Runtime.Versioning.TargetPlatformAttribute;
                Console.WriteLine($""TargetPlatform:'{attribute.PlatformName}'"");
            }
            else
            {
                Console.WriteLine(""NO TargetPlatformAttribute"");
            }

            object[] attributes = assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.SupportedOSPlatformAttribute), false);
            if (attributes.Length > 0)
            {
                var attribute = attributes[0] as System.Runtime.Versioning.SupportedOSPlatformAttribute;
                Console.WriteLine($""SupportedOSPlatform:'{attribute.PlatformName}'"");
            }
            else
            {
                Console.WriteLine(""NO SupportedOSPlatformAttribute"");
            }
        }
    }
}
";

    }
}
