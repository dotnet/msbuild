// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToReferenceAnAssembly : SdkTest
    {
        public GivenThatWeWantToReferenceAnAssembly(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netcoreapp1.0")]
        public void ItRunsAppsDirectlyReferencingAssemblies(
            string referencerTarget,
            string dependencyTarget)
        {
            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString();

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                IsSdkProject = true,
                TargetFrameworks = dependencyTarget,
            };

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dependencyProject.BuildsOnNonWindows)
            {
                return;
            }

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return ""Hello from a direct reference."";
    }
}
";

            var dependencyAsset = _testAssetsManager.CreateTestProject(dependencyProject, identifier: identifier);
            string dependencyAssemblyPath = RestoreAndBuild(dependencyAsset, dependencyProject);

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                IsSdkProject = true,
                TargetFrameworks = referencerTarget,
                // Need to use a self-contained app for now because we don't use a CLI that has a "2.0" shared framework
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(referencerTarget),
                IsExe = true,
            };
            referencerProject.References.Add(dependencyAssemblyPath);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { applicationPath })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from a direct reference.");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netcoreapp1.0")]
        public void ItRunsAppsDirectlyReferencingAssembliesWhichReferenceAssemblies(
            string referencerTarget,
            string dllDependencyTarget)
        {
            string identifier = referencerTarget.ToString() + "_" + dllDependencyTarget.ToString();

            TestProject dllDependencyProjectDependency = new TestProject()
            {
                Name = "DllDependencyDependency",
                IsSdkProject = true,
                TargetFrameworks = dllDependencyTarget,
            };

            dllDependencyProjectDependency.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return ""Hello from a reference of an indirect reference."";
    }
}
";

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProjectDependency.BuildsOnNonWindows)
            {
                return;
            }

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                IsSdkProject = true,
                TargetFrameworks = dllDependencyTarget,
            };
            dllDependencyProject.ReferencedProjects.Add(dllDependencyProjectDependency);

            dllDependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                IsSdkProject = true,
                TargetFrameworks = referencerTarget,
                // Need to use a self-contained app for now because we don't use a CLI that has a "2.0" shared framework
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(referencerTarget),
                IsExe = true,
            };
            referencerProject.References.Add(dllDependencyAssemblyPath);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { applicationPath })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from a reference of an indirect reference.");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "netstandard2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netcoreapp1.0")]
        public void ItRunsAppsReferencingAProjectDirectlyReferencingAssemblies(
            string referencerTarget,
            string dependencyTarget,
            string dllDependencyTarget)
        {
            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString();

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                IsSdkProject = true,
                TargetFrameworks = dllDependencyTarget,
            };

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProject.BuildsOnNonWindows)
            {
                return;
            }

            dllDependencyProject.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return ""Hello from an indirect reference."";
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                IsSdkProject = true,
                TargetFrameworks = dependencyTarget,
            };
            dependencyProject.References.Add(dllDependencyAssemblyPath);

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                IsSdkProject = true,
                TargetFrameworks = referencerTarget,
                // Need to use a self-contained app for now because we don't use a CLI that has a "2.0" shared framework
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(referencerTarget),
                IsExe = true,
            };
            referencerProject.ReferencedProjects.Add(dependencyProject);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { applicationPath })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from an indirect reference.");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "netstandard2.0", "net40")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netstandard1.5")]
        [InlineData("netcoreapp2.0", "netstandard2.0", "netcoreapp1.0")]
        public void ItRunsAppsReferencingAProjectDirectlyReferencingAssembliesWhichReferenceAssemblies(
            string referencerTarget,
            string dependencyTarget,
            string dllDependencyTarget)
        {
            string identifier = referencerTarget.ToString() + "_" + dependencyTarget.ToString();

            TestProject dllDependencyProjectDependency = new TestProject()
            {
                Name = "DllDependencyDependency",
                IsSdkProject = true,
                TargetFrameworks = dllDependencyTarget,
            };

            dllDependencyProjectDependency.SourceFiles["Class3.cs"] = @"
public class Class3
{
    public static string GetMessage()
    {
        return ""Hello from a reference of an indirect reference."";
    }
}
";

            //  Skip running test if not running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !dllDependencyProjectDependency.BuildsOnNonWindows)
            {
                return;
            }

            TestProject dllDependencyProject = new TestProject()
            {
                Name = "DllDependency",
                IsSdkProject = true,
                TargetFrameworks = dllDependencyTarget,
            };
            dllDependencyProject.ReferencedProjects.Add(dllDependencyProjectDependency);

            dllDependencyProject.SourceFiles["Class2.cs"] = @"
public class Class2
{
    public static string GetMessage()
    {
        return Class3.GetMessage();
    }
}
";

            var dllDependencyAsset = _testAssetsManager.CreateTestProject(dllDependencyProject, identifier: identifier);
            string dllDependencyAssemblyPath = RestoreAndBuild(dllDependencyAsset, dllDependencyProject);

            TestProject dependencyProject = new TestProject()
            {
                Name = "Dependency",
                IsSdkProject = true,
                TargetFrameworks = dependencyTarget,
            };
            dependencyProject.References.Add(dllDependencyAssemblyPath);

            dependencyProject.SourceFiles["Class1.cs"] = @"
public class Class1
{
    public static string GetMessage()
    {
        return Class2.GetMessage();
    }
}
";

            TestProject referencerProject = new TestProject()
            {
                Name = "Referencer",
                IsSdkProject = true,
                TargetFrameworks = referencerTarget,
                // Need to use a self-contained app for now because we don't use a CLI that has a "2.0" shared framework
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(referencerTarget),
                IsExe = true,
            };
            referencerProject.ReferencedProjects.Add(dependencyProject);

            referencerProject.SourceFiles["Program.cs"] = @"
using System;
public static class Program
{
    public static void Main()
    {
        Console.WriteLine(Class1.GetMessage());
    }
}
";

            var referencerAsset = _testAssetsManager.CreateTestProject(referencerProject, identifier: identifier);
            string applicationPath = RestoreAndBuild(referencerAsset, referencerProject);

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { applicationPath })
                .CaptureStdOut()
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello from a reference of an indirect reference.");
        }

        private string RestoreAndBuild(TestAsset testAsset, TestProject testProject)
        {
            testAsset.Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(
                testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier);
            return Path.Combine(outputDirectory.FullName, testProject.Name + ".dll");
        }
    }
}
