// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeWantToGetDependenciesViaDesignTimeBuild
    {
        [Fact]
        public void ItShouldNotReturnPackagesWithUnknownTypes()
        {
            var task = new PreprocessPackageDependenciesDesignTime
            {
                TargetFrameworkMoniker = ".Net Framework,Version=v4.5",
                DefaultImplicitPackages = string.Empty,
                PackageDefinitions = new ITaskItem[]
                {
                    new MockTaskItem(
                        itemSpec: "mockPackageNoType/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageNoType" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some path" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageUnknown/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageUnknown" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "qqqq" }
                        })
                },
                PackageDependencies = new ITaskItem[]
                {
                    new MockTaskItem(
                        itemSpec: "mockPackageNoType/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageUnknown/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        })
                }
            };

            Assert.True(task.Execute());

            Assert.Empty(task.PackageDependenciesDesignTime);
        }

        [Fact]
        public void ItShouldReturnUnresolvedPackageDependenciesWithTypePackage()
        {
            var task = new PreprocessPackageDependenciesDesignTime
            {
                TargetFrameworkMoniker = ".Net Framework,Version=v4.5",
                DefaultImplicitPackages = string.Empty,
                PackageDefinitions = new ITaskItem[]
                {
                    new MockTaskItem(
                        itemSpec: "mockPackageUnresolved/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageUnresolved" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "" },
                            { MetadataKeys.Type, "Unresolved" },
                            { MetadataKeys.DiagnosticLevel, "Warning" }
                        })
                },
                PackageDependencies = new ITaskItem[]
                {
                    new MockTaskItem(
                        itemSpec: "mockPackageUnresolved/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        })
                }
            };

            Assert.True(task.Execute());

            var item = Assert.Single(task.PackageDependenciesDesignTime);

            Assert.Equal("mockPackageUnresolved/1.0.0", item.ItemSpec);
            Assert.Equal("mockPackageUnresolved", item.GetMetadata(MetadataKeys.Name));
            Assert.Equal("1.0.0", item.GetMetadata(MetadataKeys.Version));
            Assert.Equal("some path", item.GetMetadata(MetadataKeys.Path));
            Assert.Equal("", item.GetMetadata(MetadataKeys.ResolvedPath));
            Assert.Equal("Warning", item.GetMetadata(MetadataKeys.DiagnosticLevel));
            Assert.False(item.GetBooleanMetadata(MetadataKeys.IsImplicitlyDefined));
            Assert.False(item.GetBooleanMetadata(PreprocessPackageDependenciesDesignTime.ResolvedMetadata));
        }

        [Fact]
        public void ItShouldIdentifyDefaultImplicitPackages()
        {
            var task = new PreprocessPackageDependenciesDesignTime
            {
                TargetFrameworkMoniker = ".Net Framework,Version=v4.5",
                DefaultImplicitPackages = "DefaultImplicit",
                PackageDefinitions = new ITaskItem[]
                {
                    new MockTaskItem(
                        itemSpec: "DefaultImplicit/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "DefaultImplicit" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "" },
                            { MetadataKeys.Type, "Package" }
                        })
                },
                PackageDependencies = new ITaskItem[]
                {
                    new MockTaskItem(
                        itemSpec: "DefaultImplicit/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        })
                }
            };

            Assert.True(task.Execute());

            var item = Assert.Single(task.PackageDependenciesDesignTime);

            Assert.Equal("DefaultImplicit/1.0.0", item.ItemSpec);
            Assert.True(item.GetBooleanMetadata(MetadataKeys.IsImplicitlyDefined));
        }

        [Fact]
        public void ItShouldIgnoreAllDependenciesWithTypeNotEqualToPackageOrUnresolved()
        {
            var task = new PreprocessPackageDependenciesDesignTime
            {
                TargetFrameworkMoniker = ".Net Framework,Version=v4.5",
                DefaultImplicitPackages = string.Empty,
                PackageDefinitions = new ITaskItem[] {
                    new MockTaskItem(
                        itemSpec: "mockPackageExternalProject/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageExternalProject" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some path" },
                            { MetadataKeys.Type, "ExternalProject" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageProject/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageProject" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Project" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageContent/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageContent" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Content" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageAssembly/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageAssembly" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Assembly" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageFrameworkAssembly/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageFrameworkAssembly" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "FrameworkAssembly" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageDiagnostic/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageDiagnostic" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Diagnostic" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageWinmd/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageWinmd" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Winmd" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageReference/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "mockPackageReference" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Reference" }
                        })
                },
                PackageDependencies = new ITaskItem[] {
                    new MockTaskItem(
                        itemSpec: "mockPackageExternalProject/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageProject/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageContent/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageAssembly/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageFrameworkAssembly/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageDiagnostic/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageWinmd/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "mockPackageReference/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        })
                }
            };

            Assert.True(task.Execute());

            Assert.Empty(task.PackageDependenciesDesignTime);
        }

        [Fact]
        public void ItShouldOnlyReturnPackagesInTheSpecifiedTarget()
        {
            var task = new PreprocessPackageDependenciesDesignTime
            {
                TargetFrameworkMoniker = ".Net Framework,Version=v4.5",
                DefaultImplicitPackages = string.Empty,
                PackageDefinitions = new ITaskItem[] {
                    new MockTaskItem(
                        itemSpec: "Package1/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "Package1" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "" },
                            { MetadataKeys.Type, "Package" }
                        }),
                    new MockTaskItem(
                        itemSpec: "Package2/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "Package2" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "" },
                            { MetadataKeys.Type, "Package" }
                        })
                },
                PackageDependencies = new ITaskItem[] {
                    new MockTaskItem(
                        itemSpec: "Package1/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "Package2/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.6" }
                        })
                }
            };

            Assert.True(task.Execute());

            var item = Assert.Single(task.PackageDependenciesDesignTime);

            Assert.Equal("Package1/1.0.0", item.ItemSpec);
        }

        [Fact]
        public void ItShouldOnlyReturnTopLevelPackages()
        {
            var task = new PreprocessPackageDependenciesDesignTime
            {
                TargetFrameworkMoniker = ".Net Framework,Version=v4.5",
                DefaultImplicitPackages = string.Empty,
                PackageDefinitions = new ITaskItem[] {
                    new MockTaskItem(
                        itemSpec: "Package1/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "Package1" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "" },
                            { MetadataKeys.Type, "Package" }
                        }),
                    new MockTaskItem(
                        itemSpec: "ChildPackage1/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.Name, "ChildPackage1" },
                            { MetadataKeys.Version, "1.0.0" },
                            { MetadataKeys.Path, "some path" },
                            { MetadataKeys.ResolvedPath, "some resolved path" },
                            { MetadataKeys.Type, "Package" }
                        })
                },
                PackageDependencies = new ITaskItem[] { 
                    new MockTaskItem(
                        itemSpec: "Package1/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" }
                        }),
                    new MockTaskItem(
                        itemSpec: "ChildPackage1/1.0.0",
                        metadata: new Dictionary<string, string>
                        {
                            { MetadataKeys.ParentTarget, ".Net Framework,Version=v4.5" },
                            { MetadataKeys.ParentPackage, "Package1/1.0.0" }
                        })
                }
            };

            Assert.True(task.Execute());

            var item = Assert.Single(task.PackageDependenciesDesignTime);

            Assert.Equal("Package1/1.0.0", item.ItemSpec);
        }
    }
}
