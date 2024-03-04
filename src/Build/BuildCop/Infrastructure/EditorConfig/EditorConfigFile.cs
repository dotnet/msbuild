// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Note:
// Code and logic is copied from the https://github.com/dotnet/roslyn/blob/06d3f153ed6af6f2b78028a1e1e6ecc55c8ff101/src/Compilers/Core/Portable/CommandLine/AnalyzerConfig.cs
// with slight changes like:
//  1. Remove dependency from Source text.
//  2. Remove support of globalconfig

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Build.BuildCop.Infrastructure.EditorConfig
{
    internal partial class EditorConfigFile
    {
        // Matches EditorConfig section header such as "[*.{js,py}]", see https://editorconfig.org for details
        private const string s_sectionMatcherPattern = @"^\s*\[(([^#;]|\\#|\\;)+)\]\s*([#;].*)?$";

        // Matches EditorConfig property such as "indent_style = space", see https://editorconfig.org for details
        private const string s_propertyMatcherPattern = @"^\s*([\w\.\-_]+)\s*[=:]\s*(.*?)\s*([#;].*)?$";

#if NETCOREAPP

    [GeneratedRegex(s_sectionMatcherPattern)]
    private static partial Regex GetSectionMatcherRegex();

    [GeneratedRegex(s_propertyMatcherPattern)]
    private static partial Regex GetPropertyMatcherRegex();

#else
        private static readonly Regex s_sectionMatcher = new Regex(s_sectionMatcherPattern, RegexOptions.Compiled);

        private static readonly Regex s_propertyMatcher = new Regex(s_propertyMatcherPattern, RegexOptions.Compiled);

        private static Regex GetSectionMatcherRegex() => s_sectionMatcher;

        private static Regex GetPropertyMatcherRegex() => s_propertyMatcher;

#endif

        internal Section GlobalSection { get; }

        /// <summary>
        /// The path passed to <see cref="Parse(string)"/> during construction.
        /// </summary>
        internal string PathToFile { get; }

        internal ImmutableArray<Section> NamedSections { get; }

        /// <summary>
        /// Gets whether this editorconfig is a topmost editorconfig.
        /// </summary>
        internal bool IsRoot => GlobalSection.Properties.TryGetValue("root", out string? val) && val == "true";

        private EditorConfigFile(
            Section globalSection,
            ImmutableArray<Section> namedSections,
            string pathToFile)
        {
            GlobalSection = globalSection;
            NamedSections = namedSections;
            PathToFile = pathToFile;
        }

        /// <summary>
        /// Parses an editor config file text located at the given path. No parsing
        /// errors are reported. If any line contains a parse error, it is dropped.
        /// </summary>
        internal static EditorConfigFile Parse(string pathToFile)
        {
            if (pathToFile is null || !Path.IsPathRooted(pathToFile) || string.IsNullOrEmpty(Path.GetFileName(pathToFile)) || !File.Exists(pathToFile))
            {
                throw new ArgumentException("Must be an absolute path to an editorconfig file", nameof(pathToFile));
            }

            Section? globalSection = null;
            var namedSectionBuilder = ImmutableArray.CreateBuilder<Section>();

            // N.B. The editorconfig documentation is quite loose on property interpretation.
            // Specifically, it says:
            //      Currently all properties and values are case-insensitive.
            //      They are lowercased when parsed.
            // To accommodate this, we use a lower case Unicode mapping when adding to the
            // dictionary, but we also use a case-insensitive key comparer when doing lookups
            var activeSectionProperties = ImmutableDictionary.CreateBuilder<string, string>();
            string activeSectionName = "";

            using (StreamReader sr = new StreamReader(pathToFile))
            {
                while (sr.Peek() >= 0)
                {
                    string? line = sr.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (IsComment(line))
                    {
                        continue;
                    }

                    var sectionMatches = GetSectionMatcherRegex().Matches(line);
                    if (sectionMatches.Count > 0 && sectionMatches[0].Groups.Count > 0)
                    {
                        addNewSection();

                        var sectionName = sectionMatches[0].Groups[1].Value;
                        Debug.Assert(!string.IsNullOrEmpty(sectionName));

                        activeSectionName = sectionName;
                        activeSectionProperties = ImmutableDictionary.CreateBuilder<string, string>();
                        continue;
                    }

                    var propMatches = GetPropertyMatcherRegex().Matches(line);
                    if (propMatches.Count > 0 && propMatches[0].Groups.Count > 1)
                    {
                        var key = propMatches[0].Groups[1].Value.ToLower();
                        var value = propMatches[0].Groups[2].Value.ToLower();

                        Debug.Assert(!string.IsNullOrEmpty(key));
                        Debug.Assert(key == key.Trim());
                        Debug.Assert(value == value?.Trim());

                        activeSectionProperties[key] = value ?? "";
                        continue;
                    }
                }
            }

            // Add the last section
            addNewSection();

            return new EditorConfigFile(globalSection!, namedSectionBuilder.ToImmutable(), pathToFile);

            void addNewSection()
            {
                // Close out the previous section
                var previousSection = new Section(activeSectionName, activeSectionProperties.ToImmutable());
                if (activeSectionName == "")
                {
                    // This is the global section
                    globalSection = previousSection;
                }
                else
                {
                    namedSectionBuilder.Add(previousSection);
                }
            }
        }

        private static bool IsComment(string line)
        {
            foreach (char c in line)
            {
                if (!char.IsWhiteSpace(c))
                {
                    return c == '#' || c == ';';
                }
            }

            return false;
        }

        /// <summary>
        /// Represents a named section of the editorconfig file, which consists of a name followed by a set
        /// of key-value pairs.
        /// </summary>
        internal sealed class Section
        {
            public Section(string name, ImmutableDictionary<string, string> properties)
            {
                Name = name;
                Properties = properties;
            }

            /// <summary>
            /// For regular files, the name as present directly in the section specification of the editorconfig file. For sections in
            /// global configs, this is the unescaped full file path.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Keys and values for this section. All keys are lower-cased according to the
            /// EditorConfig specification and keys are compared case-insensitively. Otherwise,
            /// the values are the literal values present in the source.
            /// </summary>
            public ImmutableDictionary<string, string> Properties { get; }
        }
    }
}
