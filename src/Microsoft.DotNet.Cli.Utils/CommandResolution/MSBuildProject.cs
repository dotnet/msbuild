// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class MSBuildProject : IProject
    {
        private Project _project;

        private string _msBuildExePath;

        public string DepsJsonPath
        {
            get
            {
                return _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("_ProjectDepsFilePath"))
                    .EvaluatedValue;
            }
        }

        public string RuntimeConfigJsonPath
        {
            get
            {
                return _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("_ProjectRuntimeConfigFilePath"))
                    .EvaluatedValue;
            }
        }

        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { Constants.MSBUILD_EXE_PATH, _msBuildExePath }
                };
            }
        }

        public MSBuildProject(
            string msBuildProjectPath,
            NuGetFramework framework,
            string configuration,
            string outputPath,
            string msBuildExePath)
        {
            var globalProperties = new Dictionary<string, string>()
            {
               { "MSBuildExtensionsPath", Path.GetDirectoryName(msBuildExePath) }
            };

            if(framework != null)
            {
                globalProperties.Add("TargetFramework", framework.GetShortFolderName());
            }

            if(outputPath != null)
            {
                globalProperties.Add("OutputPath", outputPath);
            }

            if(configuration != null)
            {
                globalProperties.Add("Configuration", configuration);
            }

            _project = ProjectCollection.GlobalProjectCollection.LoadProject(
                msBuildProjectPath,
                globalProperties,
                null);

            _msBuildExePath = msBuildExePath;
        }

        public IEnumerable<SingleProjectInfo> GetTools()
        {
            var toolsReferences = _project.AllEvaluatedItems.Where(i => i.ItemType.Equals("DotNetCliToolReference"));
            var tools = toolsReferences.Select(t => new SingleProjectInfo(
                t.EvaluatedInclude,
                t.GetMetadataValue("Version"),
                Enumerable.Empty<ResourceAssemblyInfo>()));

            return tools;
        }

        public LockFile GetLockFile()
        {
            var lockFilePath = GetLockFilePathFromProjectLockFileProperty() ??
                GetLockFilePathFromIntermediateBaseOutputPath();

            return new LockFileFormat().Read(lockFilePath);
        }

        private string GetLockFilePathFromProjectLockFileProperty()
        {
            return _project
                .AllEvaluatedProperties
                .Where(p => p.Name.Equals("ProjectAssetsFile"))
                .Select(p => p.EvaluatedValue)
                .FirstOrDefault(p => File.Exists(p));
        }

        private string GetLockFilePathFromIntermediateBaseOutputPath()
        {
            var intermediateOutputPath = _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("BaseIntermediateOutputPath"))
                    .EvaluatedValue;
            return Path.Combine(intermediateOutputPath, "project.assets.json");
        }
    }
}