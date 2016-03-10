// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var compilationOptions = Project.GetCompilerOptions(framework, configuration);
            if (framework.IsDesktop() && compilationOptions.EmitEntryPoint.GetValueOrDefault())
            {
                OutputExtension = FileNameSuffixes.DotNet.Exe;
            }
        }

        public string BasePath { get; }

        public string Assembly
        {
            get
            {
                var compilationOptions = Project.GetCompilerOptions(Framework, Configuration);

                return Path.Combine(
                    BasePath,
                    (compilationOptions.OutputName ?? Project.Name) + OutputExtension);
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

        public virtual IEnumerable<string> Resources()
        {
            var resourceNames = Project.Files.ResourceFiles
                .Select(f => ResourceUtility.GetResourceCultureName(f.Key))
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct();

            foreach (var resourceName in resourceNames)
            {
                yield return Path.Combine(BasePath, resourceName, Project.Name + ".resources" + FileNameSuffixes.DotNet.DynamicLib);
            }
        }

        public virtual IEnumerable<string> All()
        {
            yield return Assembly;
            yield return PdbPath;
            var compilationOptions = Project.GetCompilerOptions(Framework, Configuration);
            if (compilationOptions.GenerateXmlDocumentation == true)
            {
                yield return Path.ChangeExtension(Assembly, "xml");
            }
            foreach (var resource in Resources())
            {
                yield return resource;
            }
        }
    }
}