// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigratePackageDependenciesAndToolsRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly  ProjectDependencyFinder _projectDependencyFinder;
        private string _projectDirectory;

        public MigratePackageDependenciesAndToolsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
            _projectDependencyFinder = new ProjectDependencyFinder();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            CleanExistingPackageReferences(migrationRuleInputs.OutputMSBuildProject);

            _projectDirectory = migrationSettings.ProjectDirectory;
            var project = migrationRuleInputs.DefaultProjectContext.ProjectFile;

            var tfmDependencyMap = new Dictionary<string, IEnumerable<ProjectLibraryDependency>>();
            var targetFrameworks = project.GetTargetFrameworks();

            var noFrameworkPackageReferenceItemGroup = migrationRuleInputs.OutputMSBuildProject.AddItemGroup();

            AddProjectTypeSpecificDependencies(
                migrationRuleInputs,
                migrationSettings,
                noFrameworkPackageReferenceItemGroup);
            
            // Migrate Direct Deps first
            MigrateDependencies(
                project,
                migrationRuleInputs.OutputMSBuildProject,
                null, 
                project.Dependencies,
                migrationRuleInputs.ProjectXproj,
                itemGroup: noFrameworkPackageReferenceItemGroup);
            
            MigrationTrace.Instance.WriteLine($"Migrating {targetFrameworks.Count()} target frameworks");
            foreach (var targetFramework in targetFrameworks)
            {
                MigrationTrace.Instance.WriteLine($"Migrating framework {targetFramework.FrameworkName.GetShortFolderName()}");
                
                MigrateImports(migrationRuleInputs.CommonPropertyGroup, targetFramework);

                MigrateDependencies(
                    project,
                    migrationRuleInputs.OutputMSBuildProject,
                    targetFramework.FrameworkName, 
                    targetFramework.Dependencies,
                    migrationRuleInputs.ProjectXproj);
            }

            MigrateTools(project, migrationRuleInputs.OutputMSBuildProject);
        }

        private void AddProjectTypeSpecificDependencies(
            MigrationRuleInputs migrationRuleInputs,
            MigrationSettings migrationSettings,
            ProjectItemGroupElement noFrameworkPackageReferenceItemGroup)
        {
            var project = migrationRuleInputs.DefaultProjectContext.ProjectFile;
            var type = project.GetProjectType();
            switch (type)
            {
                case ProjectType.Test:
                    _transformApplicator.Execute(
                        PackageDependencyInfoTransform().Transform(
                            new PackageDependencyInfo
                            {
                                Name = PackageConstants.TestSdkPackageName,
                                Version = ConstantPackageVersions.TestSdkPackageVersion
                            }),
                        noFrameworkPackageReferenceItemGroup,
                        mergeExisting: false);

                    if (project.TestRunner.Equals("xunit", StringComparison.OrdinalIgnoreCase))
                    {
                        _transformApplicator.Execute(
                            PackageDependencyInfoTransform().Transform(
                                new PackageDependencyInfo
                                {
                                    Name = PackageConstants.XUnitPackageName,
                                    Version = ConstantPackageVersions.XUnitPackageVersion
                                }),
                            noFrameworkPackageReferenceItemGroup,
                            mergeExisting: false);

                        _transformApplicator.Execute(
                            PackageDependencyInfoTransform().Transform(
                                new PackageDependencyInfo
                                {
                                    Name = PackageConstants.XUnitRunnerPackageName,
                                    Version = ConstantPackageVersions.XUnitRunnerPackageVersion
                                }),
                            noFrameworkPackageReferenceItemGroup,
                            mergeExisting: false);
                    }
                    else if (project.TestRunner.Equals("mstest", StringComparison.OrdinalIgnoreCase))
                    {
                        _transformApplicator.Execute(
                            PackageDependencyInfoTransform().Transform(
                                new PackageDependencyInfo
                                {
                                    Name = PackageConstants.MstestTestAdapterName,
                                    Version = ConstantPackageVersions.MstestTestAdapterVersion
                                }),
                            noFrameworkPackageReferenceItemGroup,
                            mergeExisting: false);

                        _transformApplicator.Execute(
                            PackageDependencyInfoTransform().Transform(
                                new PackageDependencyInfo
                                {
                                    Name = PackageConstants.MstestTestFrameworkName,
                                    Version = ConstantPackageVersions.MstestTestFrameworkVersion
                                }),
                            noFrameworkPackageReferenceItemGroup,
                            mergeExisting: false);
                    }
                    break;
                case ProjectType.Library:
                    if (!project.HasDependency(
                        (dep) => dep.Name.Trim().ToLower() == PackageConstants.NetStandardPackageName.ToLower()))
                    {
                        _transformApplicator.Execute(
                            PackageDependencyInfoTransform().Transform(
                                new PackageDependencyInfo
                                {
                                    Name = PackageConstants.NetStandardPackageName,
                                    Version = PackageConstants.NetStandardPackageVersion
                                }),
                            noFrameworkPackageReferenceItemGroup,
                            mergeExisting: true);
                    }
                    break;
                default:
                    break;
            }
        }

        private void MigrateImports(
            ProjectPropertyGroupElement commonPropertyGroup,
            TargetFrameworkInformation targetFramework)
        {
            var transform = ImportsTransformation.Transform(targetFramework);

            if (transform != null)
            {
                transform.Condition = targetFramework.FrameworkName.GetMSBuildCondition();
                _transformApplicator.Execute(transform, commonPropertyGroup, mergeExisting: true);
            }
            else
            {
                MigrationTrace.Instance.WriteLine($"{nameof(MigratePackageDependenciesAndToolsRule)}: imports transform null for {targetFramework.FrameworkName.GetShortFolderName()}");
            }
        }

        private void CleanExistingPackageReferences(ProjectRootElement outputMSBuildProject)
        {
            var packageRefs = outputMSBuildProject.Items.Where(i => i.ItemType == "PackageReference").ToList();

            foreach (var packageRef in packageRefs)
            {
                var parent = packageRef.Parent;
                packageRef.Parent.RemoveChild(packageRef);
                parent.RemoveIfEmpty();
            }
        }

        private void MigrateTools(
            Project project,
            ProjectRootElement output)
        {
            if (project.Tools == null || !project.Tools.Any())
            {
                return;
            }

            var itemGroup = output.AddItemGroup();

            foreach (var tool in project.Tools)
            {
                _transformApplicator.Execute(
                    ToolTransform().Transform(ToPackageDependencyInfo(
                        tool,
                        PackageConstants.ProjectToolPackages)),
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private void MigrateDependencies(
            Project project,
            ProjectRootElement output,
            NuGetFramework framework,
            IEnumerable<ProjectLibraryDependency> dependencies, 
            ProjectRootElement xproj,
            ProjectItemGroupElement itemGroup=null)
        {
            var projectDependencies = new HashSet<string>(GetAllProjectReferenceNames(project, framework, xproj));
            var packageDependencies = dependencies.Where(d => !projectDependencies.Contains(d.Name)).ToList();

            string condition = framework?.GetMSBuildCondition() ?? "";
            itemGroup = itemGroup 
                ?? output.ItemGroups.FirstOrDefault(i => i.Condition == condition) 
                ?? output.AddItemGroup();
            itemGroup.Condition = condition;

            AutoInjectImplicitProjectJsonAssemblyReferences(framework, packageDependencies);

            foreach (var packageDependency in packageDependencies)
            {
                MigrationTrace.Instance.WriteLine(packageDependency.Name);
                AddItemTransform<PackageDependencyInfo> transform;

                if (packageDependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference)
                {
                    transform = FrameworkDependencyTransform;
                }
                else
                {
                    transform = PackageDependencyInfoTransform();
                    if (packageDependency.Type.Equals(LibraryDependencyType.Build))
                    {
                        transform = transform.WithMetadata("PrivateAssets", "All");
                    }
                    else if (packageDependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                    {
                        var metadataValue = ReadLibraryIncludeFlags(packageDependency.SuppressParent);
                        transform = transform.WithMetadata("PrivateAssets", metadataValue);
                    }
                    
                    if (packageDependency.IncludeType != LibraryIncludeFlags.All)
                    {
                        var metadataValue = ReadLibraryIncludeFlags(packageDependency.IncludeType);
                        transform = transform.WithMetadata("IncludeAssets", metadataValue);
                    }
                }

                _transformApplicator.Execute(
                    transform.Transform(ToPackageDependencyInfo(
                        packageDependency,
                        PackageConstants.ProjectDependencyPackages)),
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private PackageDependencyInfo ToPackageDependencyInfo(
            ProjectLibraryDependency dependency,
            IDictionary<string, PackageDependencyInfo> dependencyToVersionMap)
        {
            var name = dependency.Name;
            var version = dependency.LibraryRange?.VersionRange?.OriginalString;

            if (dependencyToVersionMap.ContainsKey(name))
            {
                var dependencyInfo = dependencyToVersionMap[name];
                if (dependencyInfo == null)
                {
                    return null;
                }

                name = dependencyInfo.Name;
                version = dependencyInfo.Version;
            }
            
            return new PackageDependencyInfo
            {
                Name = name,
                Version = version
            };
        }

        private void AutoInjectImplicitProjectJsonAssemblyReferences(NuGetFramework framework, 
            IList<ProjectLibraryDependency> packageDependencies)
        {
            if (framework?.IsDesktop() ?? false)
            {
                InjectAssemblyReferenceIfNotPresent("System", packageDependencies);
                if (framework.Version >= new Version(4, 0))
                {
                    InjectAssemblyReferenceIfNotPresent("Microsoft.CSharp", packageDependencies);
                }
            }
        }

        private void InjectAssemblyReferenceIfNotPresent(string dependencyName, 
            IList<ProjectLibraryDependency> packageDependencies)
        {
            if (!packageDependencies.Any(dep => 
                string.Equals(dep.Name, dependencyName, StringComparison.OrdinalIgnoreCase)))
            {
                packageDependencies.Add(new ProjectLibraryDependency
                {
                    LibraryRange = new LibraryRange(dependencyName, LibraryDependencyTarget.Reference)
                });
            }
        }

        private string ReadLibraryIncludeFlags(LibraryIncludeFlags includeFlags)
        {
            if ((includeFlags ^ LibraryIncludeFlags.All) == 0)
            {
                return "All";
            }

            if ((includeFlags ^ LibraryIncludeFlags.None) == 0)
            {
                return "None";
            }

            var flagString = "";
            var allFlagsAndNames = new List<Tuple<string, LibraryIncludeFlags>>
            {
                Tuple.Create("Analyzers", LibraryIncludeFlags.Analyzers),
                Tuple.Create("Build", LibraryIncludeFlags.Build),
                Tuple.Create("Compile", LibraryIncludeFlags.Compile),
                Tuple.Create("ContentFiles", LibraryIncludeFlags.ContentFiles),
                Tuple.Create("Native", LibraryIncludeFlags.Native),
                Tuple.Create("Runtime", LibraryIncludeFlags.Runtime)
            };
            
            foreach (var flagAndName in allFlagsAndNames)
            {
                var name = flagAndName.Item1;
                var flag = flagAndName.Item2;

                if ((includeFlags & flag) == flag)
                {
                    if (!string.IsNullOrEmpty(flagString))
                    {
                        flagString += ";";
                    }
                    flagString += name;
                }
            }

            return flagString;
        }

        private IEnumerable<string> GetAllProjectReferenceNames(
            Project project,
            NuGetFramework framework,
            ProjectRootElement xproj)
        {
            var csprojReferenceItems = _projectDependencyFinder.ResolveXProjProjectDependencies(xproj);
            var migratedXProjDependencyPaths = csprojReferenceItems.SelectMany(p => p.Includes());
            var migratedXProjDependencyNames = 
                new HashSet<string>(migratedXProjDependencyPaths.Select(p => 
                    Path.GetFileNameWithoutExtension(PathUtility.GetPathWithDirectorySeparator(p))));
            var projectDependencies = _projectDependencyFinder.ResolveDirectProjectDependenciesForFramework(
                project,
                framework,
                preResolvedProjects: migratedXProjDependencyNames);

            return projectDependencies.Select(p => p.Name).Concat(migratedXProjDependencyNames);
        }

        private AddItemTransform<PackageDependencyInfo> FrameworkDependencyTransform =>
            new AddItemTransform<PackageDependencyInfo>(
                "Reference",
                dep => dep.Name,
                dep => "",
                dep => true);

        private Func<AddItemTransform<PackageDependencyInfo>> PackageDependencyInfoTransform => 
            () => new AddItemTransform<PackageDependencyInfo>(
                "PackageReference",
                dep => dep.Name,
                dep => "",
                dep => dep != null)
                .WithMetadata("Version", r => r.Version);

        private AddItemTransform<PackageDependencyInfo> SdkPackageDependencyTransform => 
            PackageDependencyInfoTransform()
                .WithMetadata("PrivateAssets", r => r.PrivateAssets, r => !string.IsNullOrEmpty(r.PrivateAssets));

        private Func<AddItemTransform<PackageDependencyInfo>> ToolTransform => 
            () => new AddItemTransform<PackageDependencyInfo>(
                "DotNetCliToolReference",
                dep => dep.Name,
                dep => "",
                dep => dep != null)
                .WithMetadata("Version", r => r.Version);

        private AddPropertyTransform<TargetFrameworkInformation> ImportsTransformation => 
            new AddPropertyTransform<TargetFrameworkInformation>(
                "PackageTargetFallback",
                t => $"$(PackageTargetFallback);{string.Join(";", t.Imports)}",
                t => t.Imports.OrEmptyIfNull().Any());
    }
}
