// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Common;
using Project = Microsoft.DotNet.ProjectModel.Project;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class MigrateProjectDependenciesRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private string _projectDirectory;

        public MigrateProjectDependenciesRule(TransformApplicator transformApplicator = null)
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
            var propertyTransformResults = new []
            {
                AutoUnifyTransform.Transform(true),
                DesignTimeAutoUnifyTransform.Transform(true)
            };

            if (projectDependencyTransformResults.Any())
            {
                // Use a new item group for the project references, but the common for properties
                var propertyGroup = migrationRuleInputs.CommonPropertyGroup;
                var itemGroup = csproj.AddItemGroup();

                foreach (var projectDependencyTransformResult in projectDependencyTransformResults)
                {
                    _transformApplicator.Execute(projectDependencyTransformResult, itemGroup);
                }

                foreach (var propertyTransformResult in propertyTransformResults)
                {
                    _transformApplicator.Execute(propertyTransformResult, propertyGroup);
                }
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
                    throw new Exception("Cannot migrate unresolved project dependency, please ensure restore has been run.");
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
