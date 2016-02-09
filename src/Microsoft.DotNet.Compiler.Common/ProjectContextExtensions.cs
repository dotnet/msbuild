// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Generic;
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
    }
}
