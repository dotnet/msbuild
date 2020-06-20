// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeHaveAPackageReferenceWithAliases : SdkTest
    {

        public GivenThatWeHaveAPackageReferenceWithAliases(ITestOutputHelper log) : base(log)
        { }

        [RequiresMSBuildVersionFact("16.7.0")]
        public void CanBuildProjectWithPackageReferencesWithConflictingTypes()
        {
            var targetFramework = "net5.0";
            var packageReferences = GetPackageReferencesWithConflictingTypes(targetFramework, "A", "B");

            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsSdkProject = true,
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
            var packagesPaths = packageReferences.Select(e => Path.GetDirectoryName(e.NupkgPath));

            testProject.AdditionalProperties.Add("RestoreSources",
                                     "$(RestoreSources);" + string.Join(";", packagesPaths));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            testProject.SourceFiles[$"{testProject.Name}.cs"] = ConflictingClassLibUsage;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [RequiresMSBuildVersionFact("16.7.0")]
        public void CanBuildProjectWithMultiplePackageReferencesWithAliases()
        {
            var targetFramework = "net5.0";

            var packageReferenceA = GetPackageReference(targetFramework, "A", ClassLibClassA);
            var packageReferenceB = GetPackageReference(targetFramework, "B", ClassLibClassB);

            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsSdkProject = true,
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

            testProject.AdditionalProperties.Add("RestoreSources",
                                     "$(RestoreSources);" + Path.GetDirectoryName(packageReferenceA.NupkgPath) + ";" + Path.GetDirectoryName(packageReferenceB.NupkgPath));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            testProject.SourceFiles[$"{testProject.Name}.cs"] = ClassLibAandBUsage;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [RequiresMSBuildVersionFact("16.7.0")]
        public void CanBuildProjectWithAPackageReferenceWithMultipleAliases()
        {
            var targetFramework = "net5.0";

            var packageReferenceA = GetPackageReference(targetFramework, "A", ClassLibMultipleClasses);

            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsSdkProject = true,
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

            testProject.AdditionalProperties.Add("RestoreSources",
                                     "$(RestoreSources);" + Path.GetDirectoryName(packageReferenceA.NupkgPath));

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            testProject.SourceFiles[$"{testProject.Name}.cs"] = ClassLibAandBUsage;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        private IEnumerable<TestPackageReference> GetPackageReferencesWithConflictingTypes(string targetFramework, params string[] packageNames)
        {
            foreach (var packageName in packageNames)
            {
                yield return GetPackageReference(targetFramework, packageName, ClassLibConflictingMethod);
            }
        }

        private TestPackageReference GetPackageReference(string targetFramework, string packageName, string projectFileContent)
        {
            var project = GetProject(targetFramework, packageName, projectFileContent);
            var packCommand = new PackCommand(Log, _testAssetsManager.CreateTestProject(project).TestRoot, packageName);

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
                IsSdkProject = true
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
