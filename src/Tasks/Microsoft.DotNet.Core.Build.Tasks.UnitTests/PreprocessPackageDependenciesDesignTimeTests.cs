// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.DotNet.Core.Build.Tasks.UnitTests
{
    public class PreprocessPackageDependenciesDesignTimeTests
    {
        [Fact]
        public void PreprocessPackageDependenciesDesignTime_Targets()
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

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal(1, task.DependenciesDesignTime.Count());

            // Target with type
            var resultTargetsWithType = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            Assert.Equal(1, resultTargetsWithType.Length);
            VerifyTargetTaskItem(DependencyType.Target, mockTargetWithType, resultTargetsWithType[0]);
        }

        [Fact]
        public void PreprocessPackageDependenciesDesignTime_Packages_Unknown()
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

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal(3, task.DependenciesDesignTime.Count());

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            Assert.Equal(1, resultTargets.Length);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);

            // package unknown, no Type, Resolved = false
            var resultPackageNoType = task.DependenciesDesignTime
                    .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/mockPackageNoType/1.0.0")).ToArray();
            Assert.Equal(1, resultPackageNoType.Length);
            VerifyTargetTaskItem(DependencyType.Unknown, mockPackageNoType, resultPackageNoType[0]);

            // Package unknown Resolved = false
            var resultPackageUnknown = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/mockPackageUnknown/1.0.0")).ToArray();
            Assert.Equal(1, resultPackageUnknown.Length);
            VerifyTargetTaskItem(DependencyType.Unknown, mockPackageUnknown, resultPackageUnknown[0]);
        }

        [Fact]
        public void PreprocessPackageDependenciesDesignTime_Packages_WhenTypeIsNotPackageOrUnknown()
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

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal(1, task.DependenciesDesignTime.Count());

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            Assert.Equal(1, resultTargets.Length);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);
        }

        [Fact]
        public void PreprocessPackageDependenciesDesignTime_Packages_ChildPackages()
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
                    { PreprocessPackageDependenciesDesignTime.DependenciesMetadata, "ChildPackage1/1.0.0;ChildPackage2/2.0.0" }
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
                    { PreprocessPackageDependenciesDesignTime.DependenciesMetadata, "ChildPackage11/1.0.0" }
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
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
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
                    { PreprocessPackageDependenciesDesignTime.ResolvedMetadata, "True" }
                });

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

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal(5, task.DependenciesDesignTime.Count());

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            Assert.Equal(1, resultTargets.Length);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);

            // Package3/1.0.0
            mockPackage.SetMetadata(MetadataKeys.Path, mockPackage.GetMetadata(MetadataKeys.ResolvedPath));
            var resultPackage = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/Package3/1.0.0")).ToArray();
            Assert.Equal(1, resultPackage.Length);
            VerifyTargetTaskItem(DependencyType.Package, mockPackage, resultPackage[0]);

            // ChildPackage1/1.0.0
            mockChildPackage1.SetMetadata(MetadataKeys.Path, mockChildPackage1.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildPackage1 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/ChildPackage1/1.0.0")).ToArray();
            Assert.Equal(1, resultChildPackage1.Length);
            VerifyTargetTaskItem(DependencyType.Package, mockChildPackage1, resultChildPackage1[0]);

            // ChildPackage11/1.0.0
            mockChildPackage11.SetMetadata(MetadataKeys.Path, mockChildPackage11.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildPackage11 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/ChildPackage11/1.0.0")).ToArray();
            Assert.Equal(1, resultChildPackage11.Length);
            VerifyTargetTaskItem(DependencyType.Package, mockChildPackage11, resultChildPackage11[0]);

            // ChildPackage2/3.0.0
            mockChildPackage2.SetMetadata(MetadataKeys.Path, mockChildPackage2.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildPackage2 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/ChildPackage2/2.0.0")).ToArray();
            Assert.Equal(1, resultChildPackage2.Length);
            VerifyTargetTaskItem(DependencyType.Package, mockChildPackage2, resultChildPackage2[0]);
        }

        [Fact]
        public void PreprocessPackageDependenciesDesignTime_Assemblies_WhenTypeIsNotAssembly()
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

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal(1, task.DependenciesDesignTime.Count());

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            Assert.Equal(1, resultTargets.Length);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);
        }

        [Fact]
        public void PreprocessPackageDependenciesDesignTime_Assemblies_ChildAssemblies()
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
                      @"mockChildAssembly1;somepath/mockChildAssembly2" }
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

            var mockPackageDep = new MockTaskItem(
                itemSpec: "Package3/1.0.0",
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
                itemSpec: "mockChildAssemblyNoCompileMetadata/2.0.0",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                    { MetadataKeys.ParentPackage, "Package3/1.0.0" }
                });

            var task = new PreprocessPackageDependenciesDesignTime();
            task.TargetDefinitions = new[] { mockTarget };
            task.PackageDefinitions = new ITaskItem[] {
                mockPackage
            };
            task.FileDefinitions = new ITaskItem[] {
                mockChildAssembly1,
                mockChildAssembly2,
                mockChildAssemblyNoCompileMetadata
            };
            task.PackageDependencies = new ITaskItem[] {
                mockPackageDep
            };
            task.FileDependencies = new ITaskItem[] {
                mockChildAssemblyDep1,
                mockChildAssemblyDep2,
                mockChildAssemblyNoCompileMetadataDep
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal(4, task.DependenciesDesignTime.Count());

            // Target with type
            var resultTargets = task.DependenciesDesignTime
                                                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5")).ToArray();
            Assert.Equal(1, resultTargets.Length);
            VerifyTargetTaskItem(DependencyType.Target, mockTarget, resultTargets[0]);

            // Package3/1.0.0
            mockPackage.SetMetadata(MetadataKeys.Path, mockPackage.GetMetadata(MetadataKeys.ResolvedPath));
            var resultPackage = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/Package3/1.0.0")).ToArray();
            Assert.Equal(1, resultPackage.Length);
            VerifyTargetTaskItem(DependencyType.Package, mockPackage, resultPackage[0]);

            // ChildAssembly1/1.0.0
            mockChildAssembly1.SetMetadata(MetadataKeys.Path, mockChildAssembly1.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildAssembly1 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/mockChildAssembly1")).ToArray();
            Assert.Equal(1, resultChildAssembly1.Length);
            VerifyTargetTaskItem(DependencyType.Assembly, mockChildAssembly1, resultChildAssembly1[0]);

            // ChildAssembly2/1.0.0
            mockChildAssembly2.SetMetadata(MetadataKeys.Path, mockChildAssembly2.GetMetadata(MetadataKeys.ResolvedPath));
            var resultChildAssembly2 = task.DependenciesDesignTime
                .Where(x => x.ItemSpec.Equals(".Net Framework,Version=v4.5/somepath/mockChildAssembly2")).ToArray();
            Assert.Equal(1, resultChildAssembly2.Length);
            VerifyTargetTaskItem(DependencyType.FrameworkAssembly, mockChildAssembly2, resultChildAssembly2[0]);
        }

        private void VerifyTargetTaskItem(DependencyType type, ITaskItem input, ITaskItem output)
        {
            Assert.Equal(type.ToString(),
                         output.GetMetadata(MetadataKeys.Type));

            // remove unnecessary metadata to keep only ones that would be in result task items
            var removeMetadata = new[] { MetadataKeys.Type, MetadataKeys.ResolvedPath };

            foreach(var rm in removeMetadata)
            {
                output.RemoveMetadata(rm);
                input.RemoveMetadata(rm);
            }

            foreach (var metadata in input.MetadataNames)
            {
                Assert.Equal(input.GetMetadata(metadata.ToString()), output.GetMetadata(metadata.ToString()));
            }
        }
    }
}
