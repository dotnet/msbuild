// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var projectExports = projectContext.CreateExporter("_").GetDependencies(LibraryType.Project);

            var projectDependencyTransformResults =
                projectExports.Select(projectExport => ProjectDependencyTransform.Transform(projectExport));

            if (projectDependencyTransformResults.Any())
            {
                AddPropertyTransformsToCommonPropertyGroup(migrationRuleInputs.CommonPropertyGroup);
                AddProjectDependenciesToNewItemGroup(csproj.AddItemGroup(), projectDependencyTransformResults);
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
    }
}
