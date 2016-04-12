// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.ProjectModel.Resources;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class CompilationOutputFiles
    {
        protected readonly Project Project;
        protected readonly string Configuration;
        protected readonly NuGetFramework Framework;

        public CompilationOutputFiles(
            string basePath,
            Project project,
            string configuration,
            NuGetFramework framework)
        {
            BasePath = basePath;
            Project = project;
            Configuration = configuration;
            Framework = framework;
            OutputExtension = FileNameSuffixes.DotNet.DynamicLib;

            var compilerOptions = Project.GetCompilerOptions(framework, configuration);
            if (framework.IsDesktop() && compilerOptions.EmitEntryPoint.GetValueOrDefault())
            {
                OutputExtension = FileNameSuffixes.DotNet.Exe;
            }
        }

        public string BasePath { get; }

        public string Assembly
        {
            get
            {
                var compilerOptions = Project.GetCompilerOptions(Framework, Configuration);

                return Path.Combine(BasePath, compilerOptions.OutputName + OutputExtension);
            }
        }

        public string PdbPath
        {
            get
            {
                return Path.ChangeExtension(Assembly, FileNameSuffixes.CurrentPlatform.ProgramDatabase);
            }
        }

        public string OutputExtension { get; }

        public virtual IEnumerable<ResourceFile> Resources()
        {
            var resourceCultureNames = GetResourceFiles()
                .Select(f => ResourceUtility.GetResourceCultureName(f))
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct();

            foreach (var resourceCultureName in resourceCultureNames)
            {
                yield return new ResourceFile(
                    Path.Combine(
                        BasePath, resourceCultureName, Project.Name + ".resources" + FileNameSuffixes.DotNet.DynamicLib),
                    resourceCultureName);
            }
        }

        public virtual IEnumerable<string> All()
        {
            yield return Assembly;
            yield return PdbPath;
            var compilerOptions = Project.GetCompilerOptions(Framework, Configuration);
            if (compilerOptions.GenerateXmlDocumentation == true)
            {
                yield return Path.ChangeExtension(Assembly, "xml");
            }
            foreach (var resource in Resources())
            {
                yield return resource.Path;
            }
        }

        private IEnumerable<string> GetResourceFiles()
        {
            var compilerOptions = Project.GetCompilerOptions(Framework, Configuration);
            if (compilerOptions.EmbedInclude == null)
            {
                return Project.Files.ResourceFiles.Keys;
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.EmbedInclude, "/", diagnostics: null);

            return includeFiles.Select(f => f.SourcePath);
        }
    }
}