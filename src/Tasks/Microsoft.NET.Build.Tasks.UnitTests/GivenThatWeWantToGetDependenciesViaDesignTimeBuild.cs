// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeWantToGetDependenciesViaDesignTimeBuild
    {
        [Fact]
        public void ItShouldReturnOnlyValidTargetsWithoutRIDs()
        {
            // Arrange 
            // target definitions 
            var mockTargetWithType = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "target" } // lower case just in case
                });

            var mockTargetWithRidToBeSkipped = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5/win7x86",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "" },
                    { MetadataKeys.TargetFrameworkMoniker, "" },
                    { MetadataKeys.FrameworkName, "" },
                    { MetadataKeys.FrameworkVersion, "" },
                    { MetadataKeys.Type, ""}
                });

            var mockTargetWithoutType = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.6",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net46" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.6" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.6" },
                });

            var mockTargetWithEmptyItemSpecToBeSkipped = new MockTaskItem(
                itemSpec: "",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "" },
                    { MetadataKeys.TargetFrameworkMoniker, "" },
                    { MetadataKeys.FrameworkName, "" },
                    { MetadataKeys.FrameworkVersion, "" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] {
                mockTargetWithType,
                mockTargetWithRidToBeSkipped,
                mockTargetWithoutType,
                mockTargetWithEmptyItemSpecToBeSkipped
            };
            task.PackageDefinitions = new ITaskItem[] { };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] { };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(1);

            var resultTargetsWithType = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargetsWithType.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTargetWithType, resultTargetsWithType[0]);
        }

        [Fact]
        public void ItShouldNotReturnPackagesWithUnknownTypes()
        {
            // Arrange 
            // target definitions 
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockPackageNoType = new MockTaskItem(
                itemSpec: "mockPackageNoType/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageNoType" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some path" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageUnknown = new MockTaskItem(
                itemSpec: "mockPackageUnknown/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageUnknown" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "qqqq" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            // package dependencies
            var mockPackageDepNoType = new MockTaskItem(
                itemSpec: "mockPackageNoType/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageDepUnknown = new MockTaskItem(
                itemSpec: "mockPackageUnknown/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] { mockPackageNoType, mockPackageUnknown };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] { mockPackageDepNoType, mockPackageDepUnknown };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(1);

            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);
        }

        [Fact]
        public void ItShouldReturnUnresolvedPackageDependenciesWithTypePackage()
        {
            // Arrange 
            // target definitions 
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockPackageUnresolved = new MockTaskItem(
                itemSpec: "mockPackageUnresolved/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageUnresolved" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "" },
                    { MetadataKeys.Type, "Unresolved" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            // package dependencies
            var mockPackageDepUnresolved = new MockTaskItem(
                itemSpec: "mockPackageUnresolved/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] { mockPackageUnresolved };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] { mockPackageDepUnresolved };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(2);

            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);

            var resultPackageUnresolved = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/mockPackageUnresolved/1.0.0")).ToArray();
            resultPackageUnresolved.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockPackageUnresolved, resultPackageUnresolved[0]);
        }

        [Fact]
        public void ItShouldIgnoreAllDependenciesWithTypeNotEqualToPackageOrUnresolved()
        {
            // Arrange 
            // target definitions 
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockPackageExternalProject = new MockTaskItem(
                itemSpec: "mockPackageExternalProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageExternalProject" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some path" },
                    { MetadataKeys.Type, "ExternalProject" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageProject = new MockTaskItem(
                itemSpec: "mockPackageProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageProject" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Project" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageContent = new MockTaskItem(
                itemSpec: "mockPackageContent/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageContent" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Content" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageAssembly = new MockTaskItem(
                itemSpec: "mockPackageAssembly/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageAssembly" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Assembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageFrameworkAssembly = new MockTaskItem(
                itemSpec: "mockPackageFrameworkAssembly/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageFrameworkAssembly" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "FrameworkAssembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageDiagnostic = new MockTaskItem(
                itemSpec: "mockPackageDiagnostic/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageDiagnostic" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Diagnostic" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageWinmd = new MockTaskItem(
                itemSpec: "mockPackageWinmd/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageWinmd" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Winmd" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackageReference = new MockTaskItem(
                itemSpec: "mockPackageReference/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockPackageReference" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Reference" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            // package dependencies
            var mockPackageExternalProjectDep = new MockTaskItem(
                itemSpec: "mockPackageExternalProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageProjectDep = new MockTaskItem(
                itemSpec: "mockPackageProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageContentDep = new MockTaskItem(
                itemSpec: "mockPackageContent/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageAssemblyDep = new MockTaskItem(
                itemSpec: "mockPackageAssembly/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageFrameworkAssemblyDep = new MockTaskItem(
                itemSpec: "mockPackageFrameworkAssembly/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageDiagnosticDep = new MockTaskItem(
                itemSpec: "mockPackageDiagnostic/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageWinmdDep = new MockTaskItem(
                itemSpec: "mockPackageWinmd/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageReferenceDep = new MockTaskItem(
                itemSpec: "mockPackageReference/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] {
                mockPackageExternalProject,
                mockPackageProject,
                mockPackageContent,
                mockPackageAssembly,
                mockPackageFrameworkAssembly,
                mockPackageWinmd,
                mockPackageReference
            };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] {
                mockPackageExternalProjectDep,
                mockPackageProjectDep,
                mockPackageContentDep,
                mockPackageAssemblyDep,
                mockPackageFrameworkAssemblyDep,
                mockPackageWinmdDep,
                mockPackageReferenceDep
            };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(1);

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);
        }

        [Fact]
        public void ItReturnsCorrectHierarchyOfDependenciesThatHaveChildren()
        {
            // Arrange 
            // target definitions 
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockPackage = new MockTaskItem(
                itemSpec: "Package3/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "Package3" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { PreprocessPackageDependenciesDesignTime.DependenciesMetadata, "ChildPackage1/1.0.0;ChildPackage2/2.0.0" },
                    { MetadataKeys.IsTopLevelDependency, "True" }
                });

            var mockChildPackage1 = new MockTaskItem(
                itemSpec: "ChildPackage1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "ChildPackage1" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { PreprocessPackageDependenciesDesignTime.DependenciesMetadata, "ChildPackage11/1.0.0" },
                    { MetadataKeys.IsTopLevelDependency, "False" }
                });

            var mockChildPackage11 = new MockTaskItem(
                itemSpec: "ChildPackage11/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "ChildPackage11" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { MetadataKeys.IsTopLevelDependency, "False" }
                });

            var mockChildPackage2 = new MockTaskItem(
                itemSpec: "ChildPackage2/2.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "ChildPackage2" },
                    { MetadataKeys.Version, "2.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { MetadataKeys.IsTopLevelDependency, "False" }
                });

            // package dependencies
            var mockPackageDep = new MockTaskItem(
                            itemSpec: "Package3/1.0.0",
                            metadata: new Dictionary<string, string>
                            {
                                { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                            });

            var mockChildPackageDep1 = new MockTaskItem(
                itemSpec: "ChildPackage1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" }
                });

            var mockChildPackageDep11 = new MockTaskItem(
                itemSpec: "ChildPackage11/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "ChildPackage1/1.0.0" }
                });

            var mockChildPackageDep2 = new MockTaskItem(
                itemSpec: "ChildPackage2/2.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] {
                mockPackage,
                mockChildPackage1,
                mockChildPackage11,
                mockChildPackage2
            };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] {
                mockPackageDep,
                mockChildPackageDep1,
                mockChildPackageDep11,
                mockChildPackageDep2
            };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(5);

            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);

            mockPackage.SetMetadata(MetadataKeys.Path, mockPackage.GetMetadata(MetadataKeys.ResolvedPath));
            var resultPackage = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/Package3/1.0.0")).ToArray();
            resultPackage.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockPackage, resultPackage[0]);

            mockChildPackage1.SetMetadata(MetadataKeys.Path, mockChildPackage1.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildPackage1 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/ChildPackage1/1.0.0")).ToArray();
            resultChildPackage1.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockChildPackage1, resultChildPackage1[0]);

            mockChildPackage11.SetMetadata(MetadataKeys.Path, mockChildPackage11.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildPackage11 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/ChildPackage11/1.0.0")).ToArray();
            resultChildPackage11.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockChildPackage11, resultChildPackage11[0]);

            mockChildPackage2.SetMetadata(MetadataKeys.Path, mockChildPackage2.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildPackage2 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/ChildPackage2/2.0.0")).ToArray();
            resultChildPackage2.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockChildPackage2, resultChildPackage2[0]);
        }

        [Fact]
        public void ItShouldIgnoreFileDependenciesThatAre_NotAssemblies_And_DontBelongToCompileTimeAssemblyGroup()
        {
            // Arrange 
            // target definitions 
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockExternalProject = new MockTaskItem(
                itemSpec: "mockExternalProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockExternalProject" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some path" },
                    { MetadataKeys.Type, "ExternalProject" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockProject = new MockTaskItem(
                itemSpec: "mockProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockProject" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Project" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockContent = new MockTaskItem(
                itemSpec: "mockContent/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockContent" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Content" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockPackage = new MockTaskItem(
                itemSpec: "mockPackage/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockAssembly" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Assembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockDiagnostic = new MockTaskItem(
                itemSpec: "mockDiagnostic/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockDiagnostic" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Diagnostic" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockWinmd = new MockTaskItem(
                itemSpec: "mockWinmd/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockWinmd" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Winmd" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            var mockReference = new MockTaskItem(
                itemSpec: "mockReference/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockReference" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Reference" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "False" }
                });

            // package dependencies
            var mockExternalProjectDep = new MockTaskItem(
                itemSpec: "mockExternalProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockProjectDep = new MockTaskItem(
                itemSpec: "mockProject/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockContentDep = new MockTaskItem(
                itemSpec: "mockContent/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageDep = new MockTaskItem(
                itemSpec: "mockPackage/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockDiagnosticDep = new MockTaskItem(
                itemSpec: "mockDiagnostic/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockWinmdDep = new MockTaskItem(
                itemSpec: "mockWinmd/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockReferenceDep = new MockTaskItem(
                itemSpec: "mockReference/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] { };
            task.FileDefinitions = new ITaskItem[] {
                mockExternalProject,
                mockProject,
                mockContent,
                mockPackage,
                mockWinmd,
                mockReference
            };
            task.PackageDependencies = new ITaskItem[] { };
            task.FileDependencies = new ITaskItem[] {
                mockExternalProjectDep,
                mockProjectDep,
                mockContentDep,
                mockPackageDep,
                mockWinmdDep,
                mockReferenceDep
            };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(1);

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);
        }

        [Fact]
        public void ItShouldReturnCorrectHierarchyWhenPackageHasChildAssemblyOrAnalyzerDependencies()
        {
            // Arrange 
            // target definitions 
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockPackage = new MockTaskItem(
                itemSpec: "Package3/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "Package3" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { PreprocessPackageDependenciesDesignTime.DependenciesMetadata,
                      @"mockChildAssembly1;somepath/mockChildAssembly2;somepath/mockChildAnalyzerAssembly" },
                    { MetadataKeys.IsImplicitlyDefined, "True" }
                });

            var mockPackage4 = new MockTaskItem(
                itemSpec: "Package4/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "Package4" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { MetadataKeys.IsImplicitlyDefined, "True" }
                });

            var mockChildAssembly1 = new MockTaskItem(
                itemSpec: @"mockChildAssembly1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockChildAssembly1" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Assembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            var mockChildAssembly2 = new MockTaskItem(
                itemSpec: @"somepath/mockChildAssembly2",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockChildAssembly2" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "FrameworkAssembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            var mockChildAssemblyNoCompileMetadata = new MockTaskItem(
                itemSpec: @"somepath/mockChildAssemblyNoCompileMetadata",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockChildAssemblyNoCompileMetadata" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Assembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            var mockChildAnalyzerAssembly = new MockTaskItem(
                itemSpec: @"somepath/mockChildAnalyzerAssembly",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "mockChildAnalyzerAssembly" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "AnalyzerAssembly" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            // package dependencies
            var mockPackageDep = new MockTaskItem(
                itemSpec: "Package3/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockPackageDep4 = new MockTaskItem(
                itemSpec: "Package4/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockChildAssemblyDep1 = new MockTaskItem(
                itemSpec: "mockChildAssembly1",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" },
                    { MetadataKeys.FileGroup, PreprocessPackageDependenciesDesignTime.CompileTimeAssemblyMetadata }
                });

            var mockChildAssemblyDep2 = new MockTaskItem(
                itemSpec: @"somepath/mockChildAssembly2",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" },
                    { MetadataKeys.FileGroup, PreprocessPackageDependenciesDesignTime.CompileTimeAssemblyMetadata }
                });

            var mockChildAssemblyNoCompileMetadataDep = new MockTaskItem(
                itemSpec: "somepath/mockChildAssemblyNoCompileMetadata",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" }
                });

            var mockChildAnalyzerAssemblyDep = new MockTaskItem(
                itemSpec: "somepath/mockChildAnalyzerAssembly",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] {
                mockPackage,
                mockPackage4
            };
            task.FileDefinitions = new ITaskItem[] {
                mockChildAssembly1,
                mockChildAssembly2,
                mockChildAssemblyNoCompileMetadata,
                mockChildAnalyzerAssembly
            };
            task.PackageDependencies = new ITaskItem[] {
                mockPackageDep,
                mockPackageDep4
            };
            task.FileDependencies = new ITaskItem[] {
                mockChildAssemblyDep1,
                mockChildAssemblyDep2,
                mockChildAssemblyNoCompileMetadataDep,
                mockChildAnalyzerAssemblyDep
            };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = "Package3;Package4";
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(6);

            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);

            mockPackage.SetMetadata(MetadataKeys.Path, mockPackage.GetMetadata(MetadataKeys.ResolvedPath));
            var resultPackage = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/Package3/1.0.0")).ToArray();
            resultPackage.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockPackage, resultPackage[0]);

            mockPackage4.SetMetadata(MetadataKeys.Path, mockPackage4.GetMetadata(MetadataKeys.ResolvedPath));
            var resultPackage4 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/Package4/1.0.0")).ToArray();
            resultPackage4.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Package, mockPackage4, resultPackage4[0]);

            mockChildAssembly1.SetMetadata(MetadataKeys.Path, mockChildAssembly1.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildAssembly1 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/mockChildAssembly1")).ToArray();
            resultChildAssembly1.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.Assembly, mockChildAssembly1, resultChildAssembly1[0]);

            mockChildAssembly2.SetMetadata(MetadataKeys.Path, mockChildAssembly2.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildAssembly2 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/somepath/mockChildAssembly2")).ToArray();
            resultChildAssembly2.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.FrameworkAssembly, mockChildAssembly2, resultChildAssembly2[0]);

            mockChildAnalyzerAssembly.SetMetadata(MetadataKeys.Path, mockChildAnalyzerAssembly.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildAnalyzerAssembly = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/somepath/mockChildAnalyzerAssembly")).ToArray();
            resultChildAnalyzerAssembly.Length.Should().Be(1);
            VerifyTargetTaskItem(DependencyType.AnalyzerAssembly, mockChildAnalyzerAssembly, resultChildAnalyzerAssembly[0]);
        }

        [Fact]
        public void ItShouldReturnCorrectPackagesForCorrespondingTarget()
        {
            // Arrange
            // target definitions
            var mockTarget = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.5",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net45" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.5" },
                    { MetadataKeys.Type, "Target" }
                });

            var mockTarget2 = new MockTaskItem(
                itemSpec: ".Net Framework,Version=v4.6",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "net46" },
                    { MetadataKeys.TargetFrameworkMoniker, ".Net Framework,Version=v4.6" },
                    { MetadataKeys.FrameworkName, ".Net Framework" },
                    { MetadataKeys.FrameworkVersion, "4.6" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var mockPackage1 = new MockTaskItem(
                itemSpec: "Package1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "Package1" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            var mockChildPackage1 = new MockTaskItem(
                itemSpec: "ChildPackage1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "ChildPackage1" },
                    { MetadataKeys.Version, "1.0.0" },
                    { MetadataKeys.Path, "some path" },
                    { MetadataKeys.ResolvedPath, "some resolved path" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" },
                    { MetadataKeys.IsTopLevelDependency, "False" }
                });

            // package dependencies
            var mockPackageDep1 = new MockTaskItem(
                itemSpec: "Package1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                });

            var mockChildPackageDep1 = new MockTaskItem(
                itemSpec: "ChildPackage1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package1/1.0.0" }
                });

            var mockPackageDep2 = new MockTaskItem(
                itemSpec: "Package1/1.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.6" }
                });


            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget, mockTarget2 };
            task.PackageDefinitions = new ITaskItem[] { mockPackage1, mockChildPackage1 };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] { mockPackageDep1, mockPackageDep2, mockChildPackageDep1 };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = string.Empty;

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(5);

            var resultTargets = task.DependenciesDesignTime
                                    .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            resultTargets.Length.Should().Be(1);

            resultTargets = task.DependenciesDesignTime
                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.6")).ToArray();
            resultTargets.Length.Should().Be(1);

            var resultPackage1 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/Package1/1.0.0")).ToArray();
            resultPackage1.Length.Should().Be(1);
            resultPackage1[0].GetMetadata(PreprocessPackageDependenciesDesignTime.DependenciesMetadata)
                             .Should().Be("ChildPackage1/1.0.0");

            resultPackage1 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.6/Package1/1.0.0")).ToArray();
            resultPackage1.Length.Should().Be(1);
            resultPackage1[0].GetMetadata(PreprocessPackageDependenciesDesignTime.DependenciesMetadata)
                             .Should().Be("");
        }

        [Fact]
        public void ItShouldCreateDependenciesForReferencesWithNuGetMetadata()
        {
            // Arrange
            // target definitions
            var netStandard20Target = new MockTaskItem(
                itemSpec: ".NETStandard,Version=v2.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "netstandard2.0" },
                    { MetadataKeys.TargetFrameworkMoniker, ".NETStandard,Version=v2.0" },
                    { MetadataKeys.FrameworkName, ".NETStandard" },
                    { MetadataKeys.FrameworkVersion, "2.0" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var myPackage = new MockTaskItem(
                itemSpec: "MyPackage/1.5.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "MyPackage" },
                    { MetadataKeys.Version, "1.5.0" },
                    { MetadataKeys.Path, "Packages\\MyPackage\\1.5.0" },
                    { MetadataKeys.ResolvedPath, "" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            // package dependencies
            var myPackageDependency = new MockTaskItem(
                itemSpec: "MyPackage/1.5.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".NETStandard,Version=v2.0" }
                });

            // references
            var referenceWithMetadata = new MockTaskItem(
                itemSpec: "Packages\\MyPackage\\1.5.0\\AnAssembly.dll",
                metadata: new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.5.0" }
                });

            var referenceWithoutMetadata = new MockTaskItem(
                itemSpec: "Packages\\MyPackage\\1.5.0\\AnotherAssembly.dll",
                metadata: new Dictionary<string, string>());

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { netStandard20Target };
            task.PackageDefinitions = new ITaskItem[] { myPackage };
            task.FileDefinitions = new ITaskItem[] {  };
            task.PackageDependencies = new ITaskItem[] { myPackageDependency };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { referenceWithMetadata, referenceWithoutMetadata };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = ".NETStandard,Version=v2.0";

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(3);

            var resultPackage = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".NETStandard,Version=v2.0/MyPackage/1.5.0")).ToArray();
            resultPackage.Length.Should().Be(1);
            resultPackage[0].GetMetadata(PreprocessPackageDependenciesDesignTime.DependenciesMetadata)
                            .Should().Be("MyPackage/1.5.0/AnAssembly.dll");
        }

        [Fact]
        public void ItShouldMakeFacadeReferencesInvisible()
        {
            // Arrange
            // target definitions
            var netStandard20Target = new MockTaskItem(
                itemSpec: ".NETStandard,Version=v2.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, "netstandard2.0" },
                    { MetadataKeys.TargetFrameworkMoniker, ".NETStandard,Version=v2.0" },
                    { MetadataKeys.FrameworkName, ".NETStandard" },
                    { MetadataKeys.FrameworkVersion, "2.0" },
                    { MetadataKeys.Type, "Target" }
                });

            // package definitions
            var myPackage = new MockTaskItem(
                itemSpec: "MyPackage/1.5.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Name, "MyPackage" },
                    { MetadataKeys.Version, "1.5.0" },
                    { MetadataKeys.Path, "Packages\\MyPackage\\1.5.0" },
                    { MetadataKeys.ResolvedPath, "" },
                    { MetadataKeys.Type, "Package" },
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

            // package dependencies
            var myPackageDependency = new MockTaskItem(
                itemSpec: "MyPackage/1.5.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".NETStandard,Version=v2.0" }
                });

            // references
            var alphaReference = new MockTaskItem(
                itemSpec: "Packages\\MyPackage\\1.5.0\\AlphaAssembly.dll",
                metadata: new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.5.0" }
                });

            var betaReference = new MockTaskItem(
                itemSpec: "Packages\\MyPackage\\1.5.0\\BetaAssembly.dll",
                metadata: new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.5.0" },
                    { "Facade", "false" }
                });

            var gammaReference = new MockTaskItem(
                itemSpec: "Packages\\MyPackage\\1.5.0\\GammaAssembly.dll",
                metadata: new Dictionary<string, string>
                {
                    { "NuGetPackageId", "MyPackage" },
                    { "NuGetPackageVersion", "1.5.0" },
                    { "Facade", "true" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { netStandard20Target };
            task.PackageDefinitions = new ITaskItem[] { myPackage };
            task.FileDefinitions = new ITaskItem[] { };
            task.PackageDependencies = new ITaskItem[] { myPackageDependency };
            task.FileDependencies = new ITaskItem[] { };
            task.References = new ITaskItem[] { alphaReference, betaReference, gammaReference };
            task.DefaultImplicitPackages = string.Empty;
            task.TargetFrameworkMoniker = ".NETStandard,Version=v2.0";

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.DependenciesDesignTime.Count().Should().Be(5);

            var alphaDependency = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".NETStandard,Version=v2.0/MyPackage/1.5.0/AlphaAssembly.dll"))
                .Single();
            alphaDependency.GetBooleanMetadata(PreprocessPackageDependenciesDesignTime.VisibleMetadata)
                .Should().BeTrue();

            var betaDependency = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".NETStandard,Version=v2.0/MyPackage/1.5.0/BetaAssembly.dll"))
                .Single();
            betaDependency.GetBooleanMetadata(PreprocessPackageDependenciesDesignTime.VisibleMetadata)
                .Should().BeTrue();

            var gammaDependency = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".NETStandard,Version=v2.0/MyPackage/1.5.0/GammaAssembly.dll"))
                .Single();
            gammaDependency.GetBooleanMetadata(PreprocessPackageDependenciesDesignTime.VisibleMetadata)
                .Should().BeFalse();
        }

        private void VerifyTargetTaskItem(DependencyType type, ITaskItem input, ITaskItem output)
        {
            type.ToString().Should().Be(output.GetMetadata(MetadataKeys.Type));

            // remove unnecessary metadata to keep only ones that would be in result task items
            var removeMetadata = new[] { MetadataKeys.Type, MetadataKeys.ResolvedPath };

            foreach (var rm in removeMetadata)
            {
                output.RemoveMetadata(rm);
                input.RemoveMetadata(rm);
            }

            foreach (var metadata in input.MetadataNames)
            {
                input.GetMetadata(metadata.ToString()).Should().Be(output.GetMetadata(metadata.ToString()));
            }
        }
    }
}
