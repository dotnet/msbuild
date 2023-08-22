// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.CommandFactory
{
    internal class MSBuildProject : IProject
    {
        private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

        private Project _project;

        private string _projectRoot;

        private string _msBuildExePath;

        public string DepsJsonPath
        {
            get
            {
                return _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("ProjectDepsFilePath"))
                    .EvaluatedValue;
            }
        }

        public string RuntimeConfigJsonPath
        {
            get
            {
                return _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("ProjectRuntimeConfigFilePath"))
                    .EvaluatedValue;
            }
        }

        public string FullOutputPath
        {
            get
            {
                return _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("TargetDir"))
                    .EvaluatedValue;
            }
        }

        public string ProjectRoot
        {
            get
            {
                return _projectRoot;
            }
        }

        public NuGetFramework DotnetCliToolTargetFramework
        {
            get
            {
                var frameworkString = _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("DotnetCliToolTargetFramework"))
                    ?.EvaluatedValue;

                if (string.IsNullOrEmpty(frameworkString))
                {
                    return s_toolPackageFramework;
                }

                return NuGetFramework.Parse(frameworkString);
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

        public string ToolDepsJsonGeneratorProject
        {
            get
            {
                var generatorProject = _project
                    .AllEvaluatedProperties
                    .FirstOrDefault(p => p.Name.Equals("ToolDepsJsonGeneratorProject"))
                    ?.EvaluatedValue;

                return generatorProject;
            }
        }

        public MSBuildProject(
            string msBuildProjectPath,
            NuGetFramework framework,
            string configuration,
            string outputPath,
            string msBuildExePath)
        {
            _projectRoot = msBuildExePath;

            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
               { "MSBuildExtensionsPath", Path.GetDirectoryName(msBuildExePath) }
            };

            if (framework != null)
            {
                globalProperties.Add("TargetFramework", framework.GetShortFolderName());
            }

            if (outputPath != null)
            {
                globalProperties.Add("OutputPath", outputPath);
            }

            if (configuration != null)
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

            return new LockFileFormat()
                .ReadWithLock(lockFilePath)
                .Result;
        }

        public bool TryGetLockFile(out LockFile lockFile)
        {
            lockFile = null;

            var lockFilePath = GetLockFilePathFromProjectLockFileProperty() ??
                GetLockFilePathFromIntermediateBaseOutputPath();

            if (lockFilePath == null)
            {
                return false;
            }

            if (!File.Exists(lockFilePath))
            {
                return false;
            }

            lockFile = new LockFileFormat()
                .ReadWithLock(lockFilePath)
                .Result;
            return true;
        }

        private string GetLockFilePathFromProjectLockFileProperty()
        {
            return _project
                .AllEvaluatedProperties
                .Where(p => p.Name.Equals("ProjectAssetsFile"))
                .Select(p => p.EvaluatedValue)
                .FirstOrDefault(p => Path.IsPathRooted(p) && File.Exists(p));
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
