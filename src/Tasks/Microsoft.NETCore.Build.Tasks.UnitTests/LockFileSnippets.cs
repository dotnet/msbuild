// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System;
using System.Linq;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    internal static class LockFileSnippets
    {
        public static string CreateLockFileSnippet(
            string[] targets,
            string[] libraries,
            string[] projectFileDependencyGroups)
        {
            return $@"{{
              ""locked"": false,
              ""version"": 2,
              ""targets"": {{{string.Join(",", targets)}}},
              ""libraries"": {{{string.Join(",", libraries)}}},
              ""projectFileDependencyGroups"": {{{string.Join(",", projectFileDependencyGroups)}}}
            }}";
        }

        public static string CreateLibrary(string nameVer, string type = "package", params string[] members)
        {
            return $@" ""{nameVer}"": {{
                ""sha512"": ""abcde=="",
                ""type"": ""{type}"",
                ""files"": [{ToStringList(members)}]
            }}";
        }

        public static string CreateTarget(string tfm, params string[] targetLibs)
        {
            return $"\"{tfm}\": {{{string.Join(",", targetLibs)}}}";
        }

        public static string CreateTargetLibrary(
            string nameVer,
            string type = "package",
            string[] dependencies = null,
            string[] frameworkAssemblies = null,
            string[] compile = null,
            string[] runtime = null)
        {
            List<string> parts = new List<string>();
            parts.Add($"\"type\": \"{type}\"");

            if (frameworkAssemblies != null)
            {
                parts.Add($"\"frameworkAssemblies\": [{ToStringList(frameworkAssemblies)}]");
            }

            Action<string, string[]> addListIfPresent = (label, list) =>
            {
                if (list != null) parts.Add($"\"{label}\": {{{string.Join(",", list)}}}");
            };

            addListIfPresent("dependencies", dependencies);
            addListIfPresent("compile", compile);
            addListIfPresent("runtime", runtime);

            return $@" ""{nameVer}"": {{
                {string.Join(",", parts)}
            }}";
        }

        public static string CreateFileItem(string path, Dictionary<string, string> metadata = null)
        {
            var metadataString = metadata == null
                ? string.Empty
                : string.Join(",", metadata.Select(kvp => $"\"{kvp.Key}\": \"{kvp.Value}\""));

            return $"\"{path}\":{{{metadataString}}}";
        }

        public static string CreateProjectFileDependencyGroup(string tfm, params string[] dependencies)
        {
            return $"\"{tfm}\": [{ToStringList(dependencies)}]";
        }

        private static string ToStringList(params string[] members)
        {
            return members == null ? string.Empty : string.Join(",", members.Select(m => $"\"{m}\""));
        }
    }
}