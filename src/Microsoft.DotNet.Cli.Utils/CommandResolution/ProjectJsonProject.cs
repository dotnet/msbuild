// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class ProjectJsonProject : IProject
    {
        private LockFile _lockFile;

        private ProjectContext _projectContext;

        private string _projectDirectory;

        private string _configuration;

        private string _outputPath;

        private string _buildBasePath;

        private NuGetFramework _framework;

        private ProjectContext ProjectContext
        {
            get
            {
                if(_projectContext == null)
                {
                    _projectContext = ProjectContext.Create(
                        _projectDirectory,
                        _framework,
                        RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());
                }

                return _projectContext;
            }
        }

        public string DepsJsonPath
        {
            get
            {
                return ProjectContext.GetOutputPaths(
                    _configuration,
                    _buildBasePath,
                    _outputPath).RuntimeFiles.DepsJson;
            }
        }

        public string RuntimeConfigJsonPath
        {
            get
            {
                return ProjectContext.GetOutputPaths(
                    _configuration,
                    _buildBasePath,
                    _outputPath).RuntimeFiles.RuntimeConfigJson;
            }
        }

        public string FullOutputPath
        {
            get
            {
                return
                    ProjectContext.GetOutputPaths(_configuration, _buildBasePath, _outputPath).RuntimeFiles.BasePath;
            }
        }

        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                return new Dictionary<string, string>();
            }
        }

        public ProjectJsonProject(
            string projectDirectory,
            NuGetFramework framework,
            string configuration,
            string buildBasePath,
            string outputPath)
        {
            var lockFilePath = Path.Combine(projectDirectory, LockFileFormat.LockFileName);
            _lockFile = new LockFileFormat().Read(lockFilePath);

            _projectDirectory = projectDirectory;
            _framework = framework;
            _configuration = configuration;
            _buildBasePath = buildBasePath;
            _outputPath = outputPath;
        }

        public LockFile GetLockFile()
        {
            return _lockFile;
        }

        public IEnumerable<SingleProjectInfo> GetTools()
        {
            var tools = _lockFile.Tools.Where(t => t.Name.Contains(".NETCoreApp")).SelectMany(t => t.Libraries);

            return tools.Select(t => new SingleProjectInfo(
                t.Name,
                t.Version.ToFullString(),
                Enumerable.Empty<ResourceAssemblyInfo>()));
        }
    }
}