// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class OutputPathCalculator
    {
        private const string ObjDirectoryName = "obj";

        private readonly Project _project;
        private readonly NuGetFramework _framework;

        private readonly string _runtimeIdentifier;

        /// <summary>
        /// Unaltered output path. Either what is passed in in the constructor, or the project directory.
        /// </summary>
        private string BaseOutputPath { get; }

        public string BaseCompilationOutputPath { get; }

        public OutputPathCalculator(
            Project project,
            NuGetFramework framework,
            string runtimeIdentifier,
            string baseOutputPath)
        {
            _project = project;
            _framework = framework;
            _runtimeIdentifier = runtimeIdentifier;

            BaseOutputPath = string.IsNullOrWhiteSpace(baseOutputPath) ? _project.ProjectDirectory : baseOutputPath;

            BaseCompilationOutputPath = string.IsNullOrWhiteSpace(baseOutputPath)
                ? Path.Combine(_project.ProjectDirectory, DirectoryNames.Bin)
                : baseOutputPath;
        }

        public string GetOutputDirectoryPath(string buildConfiguration)
        {
            var outDir = Path.Combine(BaseCompilationOutputPath,
                buildConfiguration,
                _framework.GetShortFolderName());

            if (!string.IsNullOrEmpty(_runtimeIdentifier))
            {
                outDir = Path.Combine(outDir, _runtimeIdentifier);
            }

            return outDir;
        }

        public string GetIntermediateOutputDirectoryPath(string buildConfiguration, string intermediateOutputValue)
        {
            string intermediateOutputPath;

            if (string.IsNullOrEmpty(intermediateOutputValue))
            {
                intermediateOutputPath = Path.Combine(
                    BaseOutputPath,
                    ObjDirectoryName,
                    buildConfiguration,
                    _framework.GetTwoDigitShortFolderName());
            }
            else
            {
                intermediateOutputPath = intermediateOutputValue;
            }

            return intermediateOutputPath;
        }

        public string GetAssemblyPath(string buildConfiguration)
        {
            var compilationOptions = _project.GetCompilerOptions(_framework, buildConfiguration);
            var outputExtension = FileNameSuffixes.DotNet.DynamicLib;

            if (_framework.IsDesktop() && compilationOptions.EmitEntryPoint.GetValueOrDefault())
            {
                outputExtension = FileNameSuffixes.DotNet.Exe;
            }

            return Path.Combine(
                GetOutputDirectoryPath(buildConfiguration),
                _project.Name + outputExtension);
        }

        public IEnumerable<string> GetBuildOutputs(string buildConfiguration)
        {
            var assemblyPath = GetAssemblyPath(buildConfiguration);

            yield return assemblyPath;
            yield return Path.ChangeExtension(assemblyPath, "pdb");

            var compilationOptions = _project.GetCompilerOptions(_framework, buildConfiguration);

            if (compilationOptions.GenerateXmlDocumentation == true)
            {
                yield return Path.ChangeExtension(assemblyPath, "xml");
            }

            // This should only exist in desktop framework
            var configFile = assemblyPath + ".config";

            if (File.Exists(configFile))
            {
                yield return configFile;
            }

            // Deps file
            var depsFile = GetDepsPath(buildConfiguration);

            if (File.Exists(depsFile))
            {
                yield return depsFile;
            }
        }

        public string GetDepsPath(string buildConfiguration)
        {
            return Path.Combine(GetOutputDirectoryPath(buildConfiguration), _project.Name + FileNameSuffixes.Deps);
        }

        public string GetExecutablePath(string buildConfiguration)
        {
            var extension = FileNameSuffixes.CurrentPlatform.Exe;

            // This is the check for mono, if we're not on windows and producing outputs for
            // the desktop framework then it's an exe
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _framework.IsDesktop())
            {
                extension = FileNameSuffixes.DotNet.Exe;
            }

            return Path.Combine(
                GetOutputDirectoryPath(buildConfiguration),
                _project.Name + extension);
        }

        public string GetPdbPath(string buildConfiguration)
        {
            return Path.Combine(
                GetOutputDirectoryPath(buildConfiguration),
                _project.Name + FileNameSuffixes.DotNet.ProgramDatabase);
        }
    }
}
