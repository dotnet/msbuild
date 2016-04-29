// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Abstractions;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing
{
    public static class MatcherExtensions
    {
        public static void AddExcludePatterns(this Matcher matcher, params IEnumerable<string>[] excludePatternsGroups)
        {
            foreach (var group in excludePatternsGroups)
            {
                foreach (var pattern in group)
                {
                    matcher.AddExclude(pattern);
                }
            }
        }

        public static void AddIncludePatterns(this Matcher matcher, params IEnumerable<string>[] includePatternsGroups)
        {
            foreach (var group in includePatternsGroups)
            {
                foreach (var pattern in group)
                {
                    matcher.AddInclude(pattern);
                }
            }
        }

        public static IEnumerable<string> GetResultsInFullPath(this Matcher matcher, string directoryPath)
        {
            var matches = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(directoryPath))).Files;
            var result = matches.Select(match => Path.GetFullPath(Path.Combine(directoryPath, match.Path))).ToArray();

            return result;
        }
    }
}