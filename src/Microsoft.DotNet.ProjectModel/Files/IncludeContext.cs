// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectModel.Files
{
    public class IncludeContext
    {
        private static readonly char[] PatternSeparator = new[] { ';' };

        public IncludeContext(
            string sourceBasePath,
            string option,
            JObject rawObject,
            string[] defaultBuiltInInclude,
            string[] defaultBuiltInExclude)
        {
            if (sourceBasePath == null)
            {
                throw new ArgumentNullException(nameof(sourceBasePath));
            }

            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            if (rawObject == null)
            {
                throw new ArgumentNullException(nameof(rawObject));
            }

            SourceBasePath = sourceBasePath;
            Option = option;
            var token = rawObject.Value<JToken>(option);
            if (token.Type != JTokenType.Object)
            {
                IncludePatterns = new List<string>(ExtractValues(token));
            }
            else
            {
                IncludePatterns = CreateCollection(
                    sourceBasePath, "include", ExtractValues(token.Value<JToken>("include")), literalPath: false);

                ExcludePatterns = CreateCollection(
                    sourceBasePath, "exclude", ExtractValues(token.Value<JToken>("exclude")), literalPath: false);

                IncludeFiles = CreateCollection(
                    sourceBasePath, "includeFiles", ExtractValues(token.Value<JToken>("includeFiles")), literalPath: true);

                ExcludeFiles = CreateCollection(
                    sourceBasePath, "excludeFiles", ExtractValues(token.Value<JToken>("excludeFiles")), literalPath: true);

                var builtIns = token.Value<JToken>("builtIns") as JObject;
                if (builtIns != null)
                {
                    BuiltInsInclude = CreateCollection(
                        sourceBasePath, "include", ExtractValues(builtIns.Value<JToken>("include")), literalPath: false);

                    if (defaultBuiltInInclude != null && !BuiltInsInclude.Any())
                    {
                        BuiltInsInclude = defaultBuiltInInclude.ToList();
                    }

                    BuiltInsExclude = CreateCollection(
                        sourceBasePath, "exclude", ExtractValues(builtIns.Value<JToken>("exclude")), literalPath: false);

                    if (defaultBuiltInExclude != null && !BuiltInsExclude.Any())
                    {
                        BuiltInsExclude = defaultBuiltInExclude.ToList();
                    }
                }

                var mappings = token.Value<JToken>("mappings") as JObject;
                if (mappings != null)
                {
                    Mappings = new Dictionary<string, IncludeContext>();

                    foreach (var map in mappings)
                    {
                        Mappings.Add(
                            map.Key,
                            new IncludeContext(
                                sourceBasePath,
                                map.Key,
                                mappings,
                                defaultBuiltInInclude: null,
                                defaultBuiltInExclude: null));
                    }
                }
            }
        }

        public string SourceBasePath { get; }

        public string Option { get; }

        public List<string> IncludePatterns { get; }

        public List<string> ExcludePatterns { get; }

        public List<string> IncludeFiles { get; }

        public List<string> ExcludeFiles { get; }

        public List<string> BuiltInsInclude { get; }

        public List<string> BuiltInsExclude { get; }

        public IDictionary<string, IncludeContext> Mappings { get; }

        public override bool Equals(object obj)
        {
            var other = obj as IncludeContext;
            return other != null &&
                SourceBasePath == other.SourceBasePath &&
                Option == other.Option &&
                EnumerableEquals(IncludePatterns, other.IncludePatterns) &&
                EnumerableEquals(ExcludePatterns, other.ExcludePatterns) &&
                EnumerableEquals(IncludeFiles, other.IncludeFiles) &&
                EnumerableEquals(ExcludeFiles, other.ExcludeFiles) &&
                EnumerableEquals(BuiltInsInclude, other.BuiltInsInclude) &&
                EnumerableEquals(BuiltInsExclude, other.BuiltInsExclude) &&
                EnumerableEquals(Mappings, other.Mappings);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private static bool EnumerableEquals<T>(IEnumerable<T> left, IEnumerable<T> right)
            => Enumerable.SequenceEqual(left ?? EmptyArray<T>.Value, right ?? EmptyArray<T>.Value);

        private static string[] ExtractValues(JToken token)
        {
            if (token != null)
            {
                if (token.Type == JTokenType.String)
                {
                    return new string[] { token.Value<string>() };
                }
                else if (token.Type == JTokenType.Array)
                {
                    return token.Values<string>().ToArray();
                }
            }

            return new string[0];
        }

        internal static List<string> CreateCollection(
            string projectDirectory,
            string propertyName,
            IEnumerable<string> patternsStrings,
            bool literalPath)
        {
            var patterns = patternsStrings
                .SelectMany(patternsString => GetSourcesSplit(patternsString))
                .Select(patternString =>
                    patternString.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

            foreach (var pattern in patterns)
            {
                if (Path.IsPathRooted(pattern))
                {
                    throw new InvalidOperationException($"The '{propertyName}' property cannot be a rooted path.");
                }

                if (literalPath && pattern.Contains('*'))
                {
                    throw new InvalidOperationException($"The '{propertyName}' property cannot contain wildcard characters.");
                }
            }

            return new List<string>(patterns.Select(pattern => FolderToPattern(pattern, projectDirectory)));
        }

        private static IEnumerable<string> GetSourcesSplit(string sourceDescription)
        {
            if (string.IsNullOrEmpty(sourceDescription))
            {
                return Enumerable.Empty<string>();
            }

            return sourceDescription.Split(PatternSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string FolderToPattern(string candidate, string projectDir)
        {
            // If it's already a pattern, no change is needed
            if (candidate.Contains('*'))
            {
                return candidate;
            }

            // If the given string ends with a path separator, or it is an existing directory
            // we convert this folder name to a pattern matching all files in the folder
            if (candidate.EndsWith(@"\") ||
                candidate.EndsWith("/") ||
                Directory.Exists(Path.Combine(projectDir, candidate)))
            {
                return Path.Combine(candidate, "**", "*");
            }

            // Otherwise, it represents a single file
            return candidate;
        }
    }
}
