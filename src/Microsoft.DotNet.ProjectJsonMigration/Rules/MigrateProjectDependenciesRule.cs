// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateProjectDependenciesRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private string _projectDirectory;

        public MigrateProjectDependenciesRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            _projectDirectory = migrationSettings.ProjectDirectory;


            var migratedXProjDependencyPaths = MigrateXProjProjectDependencies(migrationSettings, migrationRuleInputs);
            var migratedXProjDependencyNames = migratedXProjDependencyPaths.Select(p => Path.GetFileNameWithoutExtension(p));

            MigrateProjectJsonProjectDependencies(migrationSettings, migrationRuleInputs, migratedXProjDependencyNames);
        }

        private IEnumerable<string> MigrateXProjProjectDependencies(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var xproj = migrationRuleInputs.ProjectXproj;
            if (xproj == null)
            {
                MigrationTrace.Instance.WriteLine($"{nameof(MigrateProjectDependenciesRule)}: No xproj file given.");
                return Enumerable.Empty<string>();
            }

            var projectReferenceItems = xproj.Items.Where(i => i.ItemType == "ProjectReference");
            
            IEnumerable<string> projectReferences = new List<string>();
            foreach (var projectReferenceItem in projectReferenceItems)
            {
                projectReferences = projectReferences.Union(projectReferenceItem.Includes());
            }

            var csprojReferences = projectReferences
                .Where(p => string.Equals(Path.GetExtension(p), ".csproj", StringComparison.OrdinalIgnoreCase));
            
            MigrationTrace.Instance.WriteLine($"{nameof(MigrateProjectDependenciesRule)}: Migrating {csprojReferences.Count()} xproj to csproj references");

            var csprojReferenceTransforms = csprojReferences.Select(r => ProjectDependencyStringTransform.Transform(r));
            foreach (var csprojReferenceTransform in csprojReferenceTransforms)
            {
                _transformApplicator.Execute(csprojReferenceTransform, migrationRuleInputs.CommonItemGroup);
            }

            return csprojReferences;
        }

        public void MigrateProjectJsonProjectDependencies(
            MigrationSettings migrationSettings, 
            MigrationRuleInputs migrationRuleInputs, 
            IEnumerable<string> migratedXProjDependencyNames)
        {
            var outputMSBuildProject = migrationRuleInputs.OutputMSBuildProject;
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var projectExports = projectContext.CreateExporter("_").GetDependencies(LibraryType.Project);

            var projectDependencyTransformResults = new List<ProjectItemElement>();
            foreach (var projectExport in projectExports)
            {
                try
                {
                    projectDependencyTransformResults.Add(ProjectDependencyTransform.Transform(projectExport));
                }
                catch (MigrationException unresolvedProjectReferenceException)
                {
                    if (!migratedXProjDependencyNames.Contains(projectExport.Library.Identity.Name))
                    {
                        throw unresolvedProjectReferenceException;
                    }

                    MigrationTrace.Instance.WriteLine($"{nameof(MigrateProjectDependenciesRule)}: Ignoring unresolved project reference {projectExport.Library.Identity.Name} satisfied by xproj to csproj ProjectReference");
                }
            }
            
            if (projectDependencyTransformResults.Any())
            {
                AddPropertyTransformsToCommonPropertyGroup(migrationRuleInputs.CommonPropertyGroup);
                AddProjectDependenciesToNewItemGroup(outputMSBuildProject.AddItemGroup(), projectDependencyTransformResults);
            }
        }

        private void AddProjectDependenciesToNewItemGroup(ProjectItemGroupElement itemGroup, IEnumerable<ProjectItemElement> projectDependencyTransformResults)
        {
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

        private AddItemTransform<LibraryExport> ProjectDependencyTransform => new AddItemTransform<LibraryExport>(
            "ProjectReference",
            export => 
            {
                if (!export.Library.Resolved)
                {
                    MigrationErrorCodes.MIGRATE1014(
                        $"Unresolved project dependency ({export.Library.Identity.Name})").Throw();
                }

                var projectFile = ((ProjectDescription)export.Library).Project.ProjectFilePath;
                var projectDir = Path.GetDirectoryName(projectFile);
                var migratedProjectFileName = Path.GetFileName(projectDir) + ".csproj";
                var relativeProjectDir = PathUtility.GetRelativePath(_projectDirectory + "/", projectDir);

                return Path.Combine(relativeProjectDir, migratedProjectFileName);
            },
            export => "",
            export => true);

        private AddItemTransform<string> ProjectDependencyStringTransform => new AddItemTransform<string>(
            "ProjectReference",
            path => path,
            path => "",
            path => true);
    }
}
