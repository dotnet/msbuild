// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class ProjectContextExtensions
    {
        public static string ProjectName(this ProjectContext context) => context.RootProject.Identity.Name;

        public static string GetDisplayName(this ProjectContext context) => $"{context.RootProject.Identity.Name} ({context.TargetFramework})";

        public static CommonCompilerOptions GetLanguageSpecificCompilerOptions(this ProjectContext context, NuGetFramework framework, string configurationName)
        {
            var baseOption = context.ProjectFile.GetCompilerOptions(framework, configurationName);

            IReadOnlyList<string> defaultSuppresses;
            var compilerName = context.ProjectFile.CompilerName ?? "csc";
            if (DefaultCompilerWarningSuppresses.Suppresses.TryGetValue(compilerName, out defaultSuppresses))
            {
                baseOption.SuppressWarnings = (baseOption.SuppressWarnings ?? Enumerable.Empty<string>()).Concat(defaultSuppresses).Distinct();
            }

            return baseOption;
        }

        public static string GetSDKVersionFile(this ProjectContext context, string configuration, string buildBasePath, string outputPath)
        {
            var intermediatePath = context.GetOutputPaths(configuration, buildBasePath, outputPath).IntermediateOutputDirectoryPath;
            return Path.Combine(intermediatePath, ".SDKVersion");
        }

        public static string IncrementalCacheFile(this ProjectContext context, string configuration, string buildBasePath, string outputPath)
        {
            var intermediatePath = context.GetOutputPaths(configuration, buildBasePath, outputPath).IntermediateOutputDirectoryPath;
            return Path.Combine(intermediatePath, ".IncrementalCache");
        }

        // used in incremental compilation for the key file
        public static CommonCompilerOptions ResolveCompilationOptions(this ProjectContext context, string configuration)
        {
            var compilationOptions = context.GetLanguageSpecificCompilerOptions(context.TargetFramework, configuration);

            // Path to strong naming key in environment variable overrides path in project.json
            var environmentKeyFile = Environment.GetEnvironmentVariable(EnvironmentNames.StrongNameKeyFile);

            if (!string.IsNullOrWhiteSpace(environmentKeyFile))
            {
                compilationOptions.KeyFile = environmentKeyFile;
            }
            else if (!string.IsNullOrWhiteSpace(compilationOptions.KeyFile))
            {
                // Resolve full path to key file
                compilationOptions.KeyFile =
                    Path.GetFullPath(Path.Combine(context.ProjectFile.ProjectDirectory, compilationOptions.KeyFile));
            }
            return compilationOptions;
        }
    }
}
