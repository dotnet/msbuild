// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Note:
// Code and logic is copied from the https://github.com/dotnet/roslyn/blob/06d3f153ed6af6f2b78028a1e1e6ecc55c8ff101/src/Compilers/Core/Portable/CommandLine/AnalyzerConfig.cs
// with slight changes like:
//  1. Remove dependency from Source text.
//  2. Remove support of globalconfig
//  3. Remove the FilePath and receive only the text

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure.EditorConfig;

internal partial class EditorConfigFile
{
    // Matches EditorConfig section header such as "[*.{js,py}]", see https://editorconfig.org for details
    private const string s_sectionMatcherPattern = @"^\s*\[(([^#;]|\\#|\\;)+)\]\s*([#;].*)?$";

    // Matches EditorConfig property such as "indent_style = space", see https://editorconfig.org for details
    private const string s_propertyMatcherPattern = @"^\s*([\w\.\-_]+)\s*[=:]\s*(.*?)\s*([#;].*)?$";

#if NET
    [GeneratedRegex(s_sectionMatcherPattern)]
    private static partial Regex SectionMatcherRegex { get; }

    [GeneratedRegex(s_propertyMatcherPattern)]
    private static partial Regex PropertyMatcherRegex { get; }
#else
    private static Regex SectionMatcherRegex { get; } = new Regex(s_sectionMatcherPattern, RegexOptions.Compiled);

    private static Regex PropertyMatcherRegex { get; } = new Regex(s_propertyMatcherPattern, RegexOptions.Compiled);
#endif

    internal Section GlobalSection { get; }

    internal ImmutableArray<Section> NamedSections { get; }

    /// <summary>
    /// Gets whether this editorconfig is a topmost editorconfig.
    /// </summary>
    internal bool IsRoot => GlobalSection.Properties.TryGetValue("root", out string? val) && val?.ToLower() == "true";

    private EditorConfigFile(
        Section globalSection,
        ImmutableArray<Section> namedSections)
    {
        GlobalSection = globalSection;
        NamedSections = namedSections;
    }

    /// <summary>
    /// Parses an editor config file text located at the given path. No parsing
    /// errors are reported. If any line contains a parse error, it is dropped.
    /// </summary>
    internal static EditorConfigFile Parse(string text)
    {
        Section? globalSection = null;
        var namedSectionBuilder = ImmutableArray.CreateBuilder<Section>();

        // N.B. The editorconfig documentation is quite loose on property interpretation.
        // Specifically, it says:
        //      Currently all properties and values are case-insensitive.
        //      They are lowercased when parsed.
        // To accommodate this, we use a lower case Unicode mapping when adding to the
        // dictionary, but we also use a case-insensitive key comparer when doing lookups
        var activeSectionProperties = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
        string activeSectionName = "";
        var lines = string.IsNullOrEmpty(text) ? [] : text.Split(["\r\n", "\n"], StringSplitOptions.None);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsComment(line))
            {
                continue;
            }

            var sectionMatch = SectionMatcherRegex.Match(line);
            if (sectionMatch.Success && sectionMatch.Groups.Count > 0)
            {
                addNewSection();

                var sectionName = sectionMatch.Groups[1].Value;
                Debug.Assert(!string.IsNullOrEmpty(sectionName));

                activeSectionName = sectionName;
                activeSectionProperties = ImmutableDictionary.CreateBuilder<string, string>();
                continue;
            }

            var propMatch = PropertyMatcherRegex.Match(line);
            if (propMatch.Success && propMatch.Groups.Count > 1)
            {
                var key = propMatch.Groups[1].Value.ToLower();
                var value = propMatch.Groups[2].Value;

                Debug.Assert(!string.IsNullOrEmpty(key));
                Debug.Assert(key == key.Trim());
                Debug.Assert(value == value?.Trim());

                activeSectionProperties[key] = value ?? "";
                continue;
            }
        }

        // Add the last section
        addNewSection();

        return new EditorConfigFile(globalSection!, namedSectionBuilder.ToImmutable());

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
        /// EditorConfig specification and keys are compared case-insensitively.
        /// </summary>
        public ImmutableDictionary<string, string> Properties { get; }
    }
}
