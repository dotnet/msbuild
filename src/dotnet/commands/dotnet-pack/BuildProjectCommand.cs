// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Files;

namespace Microsoft.DotNet.Tools.Pack
{
    internal class BuildProjectCommand
    {
        private readonly Project _project;

        private readonly string _buildBasePath;
        private readonly string _configuration;

        private readonly string _versionSuffix;

        public BuildProjectCommand(
            Project project,
            string buildBasePath,
            string configuration,
            string versionSuffix)
        {
            _project = project;
            _buildBasePath = buildBasePath;
            _configuration = configuration;
            _versionSuffix = versionSuffix;
        }

        public int Execute()
        {
            if (HasSourceFiles())
            {
                var argsBuilder = new List<string>();
                argsBuilder.Add("--configuration");
                argsBuilder.Add($"{_configuration}");

                if (!string.IsNullOrEmpty(_versionSuffix))
                {
                    argsBuilder.Add("--version-suffix");
                    argsBuilder.Add(_versionSuffix);
                }

                if (!string.IsNullOrEmpty(_buildBasePath))
                {
                    argsBuilder.Add("--build-base-path");
                    argsBuilder.Add($"{_buildBasePath}");
                }

                argsBuilder.Add($"{_project.ProjectFilePath}");

                var result = Build.BuildCommand.Run(argsBuilder.ToArray());

                return result;
            }

            return 0;
        }

        private bool HasSourceFiles()
        {
            var compilerOptions = _project.GetCompilerOptions(
                _project.GetTargetFramework(targetFramework: null).FrameworkName, _configuration);

            if (compilerOptions.CompileInclude == null)
            {
                return _project.Files.SourceFiles.Any();
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Any();
        }
    }
}
