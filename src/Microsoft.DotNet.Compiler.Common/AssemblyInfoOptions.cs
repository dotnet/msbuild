// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;
using System;
using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.Dotnet.Cli.Compiler.Common
{
    public class AssemblyInfoOptions
    {
        private const string TitleOptionName = "title";

        private const string DescriptionOptionName = "description";

        private const string CopyrightOptionName = "copyright";

        private const string AssemblyFileVersionOptionName = "file-version";

        private const string AssemblyVersionOptionName = "version";

        private const string InformationalVersionOptionName = "informational-version";

        private const string CultureOptionName = "culture";

        private const string NeutralCultureOptionName = "neutral-language";

        public string Title { get; set; }

        public string Description { get; set; }

        public string Copyright { get; set; }

        public string AssemblyFileVersion { get; set; }

        public string AssemblyVersion { get; set; }

        public string InformationalVersion { get; set; }

        public string Culture { get; set; }

        public string NeutralLanguage { get; set; }

        public static AssemblyInfoOptions CreateForProject(Project project)
        {
            return new AssemblyInfoOptions()
            {
                AssemblyVersion = project.Version?.Version.ToString(),
                AssemblyFileVersion = project.AssemblyFileVersion.ToString(),
                InformationalVersion = project.Version.ToString(),
                Copyright = project.Copyright,
                Description = project.Description,
                Title = project.Title,
                NeutralLanguage = project.Language
            };
        }

        public static AssemblyInfoOptions Parse(ArgumentSyntax syntax)
        {
            string version = null;
            string informationalVersion = null;
            string fileVersion = null;
            string title = null;
            string description = null;
            string copyright = null;
            string culture = null;
            string neutralCulture = null;

            syntax.DefineOption(AssemblyVersionOptionName, ref version, "Assembly version");

            syntax.DefineOption(TitleOptionName, ref title, "Assembly title");

            syntax.DefineOption(DescriptionOptionName, ref description, "Assembly description");

            syntax.DefineOption(CopyrightOptionName, ref copyright, "Assembly copyright");

            syntax.DefineOption(NeutralCultureOptionName, ref neutralCulture, "Assembly neutral culture");

            syntax.DefineOption(CultureOptionName, ref culture, "Assembly culture");

            syntax.DefineOption(InformationalVersionOptionName, ref informationalVersion, "Assembly informational version");

            syntax.DefineOption(AssemblyFileVersionOptionName, ref fileVersion, "Assembly title");

            return new AssemblyInfoOptions()
            {
                AssemblyFileVersion = fileVersion,
                AssemblyVersion = version,
                Copyright = copyright,
                NeutralLanguage = neutralCulture,
                Description = description,
                InformationalVersion = informationalVersion,
                Title = title
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

            return options;
        }

        private static string FormatOption(string optionName, string value)
        {
            return $"--{optionName}:{value}";
        }
    }
}
