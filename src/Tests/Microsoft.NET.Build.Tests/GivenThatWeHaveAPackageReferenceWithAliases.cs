// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeHaveAPackageReferenceWithAliases : SdkTest
    {

        public GivenThatWeHaveAPackageReferenceWithAliases(ITestOutputHelper log) : base(log)
        { }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void CanBuildProjectWithPackageReferencesWithConflictingTypes()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var packageReferences = GetPackageReferencesWithConflictingTypes(targetFramework, packageNames: new string[] { "A", "B" });

            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = targetFramework,
            };

            testProject.PackageReferences.Add(packageReferences.First());
            testProject.PackageReferences.Add(
                new TestPackageReference(
                    packageReferences.Last().ID,
                    packageReferences.Last().Version,
                    packageReferences.Last().NupkgPath,
                    packageReferences.Last().PrivateAssets,
                    aliases: "Special"));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            testProject.SourceFiles[$"{testProject.Name}.cs"] = ConflictingClassLibUsage;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var packagesPaths = packageReferences.Select(e => Path.GetDirectoryName(e.NupkgPath));
            List<string> sources = new() { NuGetConfigWriter.DotnetCoreBlobFeed };
            sources.AddRange(packagesPaths);
            NuGetConfigWriter.Write(testAsset.TestRoot, sources);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void CanBuildProjectWithMultiplePackageReferencesWithAliases()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;

            var packageReferenceA = GetPackageReference(targetFramework, "A", ClassLibClassA);
            var packageReferenceB = GetPackageReference(targetFramework, "B", ClassLibClassB);

            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = targetFramework,
            };

            testProject.PackageReferences.Add(
               new TestPackageReference(
                   packageReferenceA.ID,
                   packageReferenceA.Version,
                   packageReferenceA.NupkgPath,
                   packageReferenceA.PrivateAssets,
                   aliases: "First"));
            testProject.PackageReferences.Add(
               new TestPackageReference(
                   packageReferenceB.ID,
                   packageReferenceB.Version,
                   packageReferenceB.NupkgPath,
                   packageReferenceB.PrivateAssets,
                   aliases: "Second"));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            testProject.SourceFiles[$"{testProject.Name}.cs"] = ClassLibAandBUsage;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            List<string> sources = new() { NuGetConfigWriter.DotnetCoreBlobFeed, Path.GetDirectoryName(packageReferenceA.NupkgPath), Path.GetDirectoryName(packageReferenceB.NupkgPath) };
            NuGetConfigWriter.Write(testAsset.TestRoot, sources);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void CanBuildProjectWithAPackageReferenceWithMultipleAliases()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;

            var packageReferenceA = GetPackageReference(targetFramework, "A", ClassLibMultipleClasses);

            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = targetFramework,
            };

            testProject.PackageReferences.Add(
               new TestPackageReference(
                   packageReferenceA.ID,
                   packageReferenceA.Version,
                   packageReferenceA.NupkgPath,
                   packageReferenceA.PrivateAssets,
                   aliases: "First,Second"));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            testProject.SourceFiles[$"{testProject.Name}.cs"] = ClassLibAandBUsage;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            List<string> sources = new() { NuGetConfigWriter.DotnetCoreBlobFeed, Path.GetDirectoryName(packageReferenceA.NupkgPath) };
            NuGetConfigWriter.Write(testAsset.TestRoot, sources);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        private IEnumerable<TestPackageReference> GetPackageReferencesWithConflictingTypes(string targetFramework, string[] packageNames, [CallerMemberName] string callingMethod = "")
        {
            var result = new List<TestPackageReference>();
            foreach (var packageName in packageNames)
            {
                result.Add(GetPackageReference(targetFramework, packageName, ClassLibConflictingMethod, callingMethod, packageName));
            }
            return result;
        }

        private TestPackageReference GetPackageReference(string targetFramework, string packageName, string projectFileContent, [CallerMemberName] string callingMethod = "", string identifier = null)
        {
            var project = GetProject(targetFramework, packageName, projectFileContent);
            var packCommand = new PackCommand(_testAssetsManager.CreateTestProject(project, callingMethod: callingMethod, identifier: identifier));

            packCommand
                .Execute()
                .Should()
                .Pass();
            return new TestPackageReference(packageName, "1.0.0", packCommand.GetNuGetPackage(packageName));
        }

        private static TestProject GetProject(string targetFramework, string referenceProjectName, string projectFileContent)
        {
            var project = new TestProject()
            {
                Name = referenceProjectName,
                TargetFrameworks = targetFramework,
            };
            project.SourceFiles[$"{referenceProjectName}.cs"] = projectFileContent;
            return project;
        }

        private static string ClassLibConflictingMethod = @"
using System;
public class ClassLib
{
    public void ConflictingMethod()
    {
    }
}
";

        private static string ClassLibClassA = @"
using System;
public class A
{
    public void AMethod()
    {
    }
}
";

        private static string ClassLibMultipleClasses = @"
using System;
public class A
{
    public void AMethod()
    {
    }
}

public class B
{
    public void BMethod()
    {
    }
}
";

        private static string ClassLibClassB = @"
using System;
public class B
{
    public void BMethod()
    {
    }
}
";

        private static string ClassLibAandBUsage = @"
extern alias First;
extern alias Second;
using System;
public class ClassLibUsage
{
    public void UsageMethod()
    {
        new First.A().AMethod();
        new Second.B().BMethod();
    }
}
";

        private static string ConflictingClassLibUsage = @"
extern alias Special;
using System;
public class ClassLibUsage
{
    public void UsageMethod()
    {
        new Special.ClassLib().ConflictingMethod();
    }
}
";
    }
}
