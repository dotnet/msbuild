// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigratePackageDependenciesRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly  ProjectDependencyFinder _projectDependencyFinder;
        private string _projectDirectory;

        public MigratePackageDependenciesRule(ITransformApplicator transformApplicator = null)
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

            // Inject Sdk dependency
            PackageDependencyInfoTransform.Transform(new PackageDependencyInfo
                {
                    Name = ConstantPackageNames.CSdkPackageName,
                    Version = migrationSettings.SdkPackageVersion
                });
            
            // Migrate Direct Deps first
            MigrateDependencies(
                project,
                migrationRuleInputs.OutputMSBuildProject,
                null, 
                project.Dependencies,
                migrationRuleInputs.ProjectXproj);

            foreach (var targetFramework in targetFrameworks)
            {
                MigrateImports(migrationRuleInputs.CommonItemGroup, targetFramework);

                MigrateDependencies(
                    project,
                    migrationRuleInputs.OutputMSBuildProject,
                    targetFramework.FrameworkName, 
                    project.Dependencies,
                    migrationRuleInputs.ProjectXproj); 
            }
        }

        private void MigrateImports(ProjectItemGroupElement commonItemGroup, TargetFrameworkInformation targetFramework)
        {
            var transform = ImportsTransformation.Transform(targetFramework);
            if (transform != null)
            {
                transform.Condition = targetFramework.FrameworkName.GetMSBuildCondition();
                _transformApplicator.Execute(transform, commonItemGroup);
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

        private void MigrateDependencies(
            Project project,
            ProjectRootElement output,
            NuGetFramework framework,
            IEnumerable<ProjectLibraryDependency> dependencies, 
            ProjectRootElement xproj)
        {
            var projectDependencies = new HashSet<string>(GetAllProjectReferenceNames(project, framework, xproj));
            var packageDependencies = dependencies.Where(d => !projectDependencies.Contains(d.Name));

            string condition = framework?.GetMSBuildCondition() ?? "";
            var itemGroup = output.ItemGroups.FirstOrDefault(i => i.Condition == condition) 
                ?? output.AddItemGroup();
            itemGroup.Condition = condition;

            foreach (var packageDependency in packageDependencies)
            {    
                AddItemTransform<ProjectLibraryDependency> transform;

                if (packageDependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference)
                {
                    transform = FrameworkDependencyTransform;
                }
                else
                {
                    transform = PackageDependencyTransform();
                    if (packageDependency.Type == LibraryDependencyType.Build)
                    {
                        Console.WriteLine("Build type!!!");
                        transform = transform.WithMetadata("PrivateAssets", "all");
                    }
                    else if (packageDependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                    {
                        var metadataValue = ReadLibraryIncludeFlags(packageDependency.SuppressParent);
                        transform = transform.WithMetadata("PrivateAssets", metadataValue);
                    }
                    
                    // TODO: include/exclude
                    if (packageDependency.IncludeType != LibraryIncludeFlags.All)
                    {
                        var metadataValue = ReadLibraryIncludeFlags(packageDependency.IncludeType);
                        transform = transform.WithMetadata("IncludeAssets", metadataValue);
                    }
                }

                _transformApplicator.Execute(transform.Transform(packageDependency), itemGroup);
            }
        }

        private string ReadLibraryIncludeFlags(LibraryIncludeFlags includeFlags)
        {
            if ((includeFlags & LibraryIncludeFlags.All) == LibraryIncludeFlags.All)
            {
                return "All";
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

        private IEnumerable<string> GetAllProjectReferenceNames(Project project, NuGetFramework framework, ProjectRootElement xproj)
        {
            var csprojReferenceItems = _projectDependencyFinder.ResolveXProjProjectDependencies(xproj);
            var migratedXProjDependencyPaths = csprojReferenceItems.SelectMany(p => p.Includes());
            var migratedXProjDependencyNames = new HashSet<string>(migratedXProjDependencyPaths.Select(p => Path.GetFileNameWithoutExtension(
                                                                                                                 PathUtility.GetPathWithDirectorySeparator(p))));
            var projectDependencies = _projectDependencyFinder.ResolveProjectDependenciesForFramework(
                project,
                framework,
                preResolvedProjects: migratedXProjDependencyNames);

            return projectDependencies.Select(p => p.Name).Concat(migratedXProjDependencyNames);
        }

         private AddItemTransform<ProjectLibraryDependency> FrameworkDependencyTransform => new AddItemTransform<ProjectLibraryDependency>(
            "Reference",
            dep => dep.Name,
            dep => "",
            dep => true);

        private Func<AddItemTransform<ProjectLibraryDependency>> PackageDependencyTransform => () => new AddItemTransform<ProjectLibraryDependency>(
            "PackageReference",
            dep => dep.Name,
            dep => "",
            dep => true)
            .WithMetadata("Version", r => r.LibraryRange.VersionRange.OriginalString);

        private AddItemTransform<PackageDependencyInfo> PackageDependencyInfoTransform => new AddItemTransform<PackageDependencyInfo>(
            "PackageReference",
            dep => dep.Name,
            dep => "",
            dep => true)
            .WithMetadata("Version", r => r.Version);

        private AddItemTransform<TargetFrameworkInformation> ImportsTransformation => new AddItemTransform<TargetFrameworkInformation>(
            "PackageTargetFallback",
            t => $"$(PackageTargetFallback);{string.Join(";", t.Imports)}",
            t => "",
            t => t.Imports.Any());

        private class PackageDependencyInfo
        {
            public string Name {get; set;}
            public string Version {get; set;}
        }
    }
}
