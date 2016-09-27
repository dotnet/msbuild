// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateProjectDependenciesRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly  ProjectDependencyFinder _projectDependencyFinder;
        private string _projectDirectory;

        public MigrateProjectDependenciesRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
            _projectDependencyFinder = new ProjectDependencyFinder();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            _projectDirectory = migrationSettings.ProjectDirectory;

            var migratedXProjDependencyPaths = MigrateXProjProjectDependencies(migrationRuleInputs);
            var migratedXProjDependencyNames = new HashSet<string>(migratedXProjDependencyPaths.Select(p => Path.GetFileNameWithoutExtension(
                                                                                                                 PathUtility.GetPathWithDirectorySeparator(p))));

            AddPropertyTransformsToCommonPropertyGroup(migrationRuleInputs.CommonPropertyGroup);
            MigrateProjectJsonProjectDependencies(
                migrationRuleInputs.ProjectContexts, 
                migratedXProjDependencyNames, 
                migrationRuleInputs.OutputMSBuildProject);
        }

        private IEnumerable<string> MigrateXProjProjectDependencies(MigrationRuleInputs migrationRuleInputs)
        {
            var csprojReferenceItems = _projectDependencyFinder.ResolveXProjProjectDependencies(migrationRuleInputs.ProjectXproj);

            if (!csprojReferenceItems.Any())
            {
                return Enumerable.Empty<string>();
            }

            var csprojTransformedReferences = new List<ProjectItemElement>();

            foreach (var csprojReferenceItem in csprojReferenceItems)
            {
                var conditionChain = csprojReferenceItem.ConditionChain();
                var condition = string.Join(" and ", conditionChain);

                var referenceInclude = string.Join(";", csprojReferenceItem.Includes()
                    .Where(include => 
                        string.Equals(Path.GetExtension(include), ".csproj", StringComparison.OrdinalIgnoreCase)));

                var transformItem = ProjectDependencyStringTransform.Transform(referenceInclude);
                transformItem.Condition = condition; 

                csprojTransformedReferences.Add(transformItem);
            }

            MigrationTrace.Instance.WriteLine($"{nameof(MigrateProjectDependenciesRule)}: Migrating {csprojTransformedReferences.Count()} xproj to csproj references");

            foreach (var csprojTransformedReference in csprojTransformedReferences)
            {
                _transformApplicator.Execute(csprojTransformedReference, migrationRuleInputs.CommonItemGroup);
            }

            return csprojTransformedReferences.SelectMany(r => r.Includes());
        }

        public void MigrateProjectJsonProjectDependencies(
            IEnumerable<ProjectContext> projectContexts,
            HashSet<string> migratedXProjDependencyNames,
            ProjectRootElement outputMSBuildProject)
        {
            foreach (var projectContext in projectContexts)
            {
                var projectDependencies = _projectDependencyFinder.ResolveProjectDependencies(projectContext, migratedXProjDependencyNames);

                var projectDependencyTransformResults = projectDependencies.Select(p => ProjectDependencyTransform.Transform(p));
                
                if (projectDependencyTransformResults.Any())
                {
                    AddProjectDependenciesToNewItemGroup(
                        outputMSBuildProject.AddItemGroup(), 
                        projectDependencyTransformResults, 
                        projectContext.TargetFramework);
                }
            }
            
        }

        private void AddProjectDependenciesToNewItemGroup(
            ProjectItemGroupElement itemGroup, 
            IEnumerable<ProjectItemElement> projectDependencyTransformResults,
            NuGetFramework targetFramework)
        {
            if (targetFramework != null)
            {
                itemGroup.Condition = $" '$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)' == '{targetFramework.DotNetFrameworkName}' ";
            }

            foreach (var projectDependencyTransformResult in projectDependencyTransformResults)
            {
                _transformApplicator.Execute(projectDependencyTransformResult, itemGroup);
            }
        }

        private void AddPropertyTransformsToCommonPropertyGroup(ProjectPropertyGroupElement commonPropertyGroup)
        {
            var propertyTransformResults = new[]
            {
                AutoUnifyTransform.Transform(true),
                DesignTimeAutoUnifyTransform.Transform(true)
            };

            foreach (var propertyTransformResult in propertyTransformResults)
            {
                _transformApplicator.Execute(propertyTransformResult, commonPropertyGroup);
            }
        }

        private AddPropertyTransform<bool> AutoUnifyTransform => new AddPropertyTransform<bool>(
            "AutoUnify",
            "true",
            b => true);

        private AddPropertyTransform<bool> DesignTimeAutoUnifyTransform => new AddPropertyTransform<bool>(
            "DesignTimeAutoUnify",
            "true",
            b => true);

        private AddItemTransform<ProjectDependency> ProjectDependencyTransform => new AddItemTransform<ProjectDependency>(
            "ProjectReference",
            dep => 
            {
                var projectDir = Path.GetDirectoryName(dep.ProjectFilePath);
                var migratedProjectFileName = Path.GetFileName(projectDir) + ".csproj";
                var relativeProjectDir = PathUtility.GetRelativePath(_projectDirectory + "/", projectDir);

                return Path.Combine(relativeProjectDir, migratedProjectFileName);
            },
            dep => "",
            dep => true);

        private AddItemTransform<string> ProjectDependencyStringTransform => new AddItemTransform<string>(
            "ProjectReference",
            path => path,
            path => "",
            path => true);
    }
}
