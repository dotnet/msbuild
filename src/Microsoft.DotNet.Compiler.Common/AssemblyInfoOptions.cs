// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public class AssemblyInfoOptions
    {
        public static readonly string TitleOptionName = "title";

        public static readonly string DescriptionOptionName = "description";

        public static readonly string CopyrightOptionName = "copyright";

        public static readonly string AssemblyFileVersionOptionName = "file-version";

        public static readonly string AssemblyVersionOptionName = "version";

        public static readonly string InformationalVersionOptionName = "informational-version";

        public static readonly string CultureOptionName = "culture";

        public static readonly string NeutralCultureOptionName = "neutral-language";

        public static readonly string TargetFrameworkOptionName = "target-framework";

        public string Title { get; set; }

        public string Description { get; set; }

        public string Copyright { get; set; }

        public string AssemblyFileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public string InformationalVersion { get; set; }

        public string Culture { get; set; }

        public string NeutralLanguage { get; set; }

        public string TargetFramework { get; set; }

        public static AssemblyInfoOptions CreateForProject(ProjectContext context)
        {
            var project = context.ProjectFile;
            NuGetFramework targetFramework = null;
            // force .NETFramework instead of DNX
            if (context.TargetFramework.IsDesktop())
            {
                targetFramework = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, context.TargetFramework.Version);
            }
            else
            {
                targetFramework = context.TargetFramework;
            }

            return new AssemblyInfoOptions()
            {
                AssemblyVersion = project.Version?.Version.ToString(),
                AssemblyFileVersion = project.AssemblyFileVersion.ToString(),
                InformationalVersion = project.Version.ToString(),
                Copyright = project.Copyright,
                Description = project.Description,
                Title = project.Title,
                NeutralLanguage = project.Language,
                TargetFramework = targetFramework.DotNetFrameworkName
            };
        }

        public static IEnumerable<string> SerializeToArgs(AssemblyInfoOptions assemblyInfoOptions)
        {
            var options = new List<string>();

            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.Title))
            {
                options.Add(FormatOption(TitleOptionName, assemblyInfoOptions.Title));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.Description))
            {
                options.Add(FormatOption(DescriptionOptionName, assemblyInfoOptions.Description));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.Copyright))
            {
                options.Add(FormatOption(CopyrightOptionName, assemblyInfoOptions.Copyright));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.AssemblyFileVersion))
            {
                options.Add(FormatOption(AssemblyFileVersionOptionName, assemblyInfoOptions.AssemblyFileVersion));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.AssemblyVersion))
            {
                options.Add(FormatOption(AssemblyVersionOptionName, assemblyInfoOptions.AssemblyVersion));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.InformationalVersion))
            {
                options.Add(FormatOption(InformationalVersionOptionName, assemblyInfoOptions.InformationalVersion));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.Culture))
            {
                options.Add(FormatOption(CultureOptionName, assemblyInfoOptions.Culture));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.NeutralLanguage))
            {
                options.Add(FormatOption(NeutralCultureOptionName, assemblyInfoOptions.NeutralLanguage));
            }
            if (!string.IsNullOrWhiteSpace(assemblyInfoOptions.TargetFramework))
            {
                options.Add(FormatOption(TargetFrameworkOptionName, assemblyInfoOptions.TargetFramework));
            }

            return options;
        }

        private static string FormatOption(string optionName, string value)
        {
            return $"--{optionName}:{EscapeNewlines(value)}";
        }

        private static string EscapeNewlines(string text)
        {
            return text.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
