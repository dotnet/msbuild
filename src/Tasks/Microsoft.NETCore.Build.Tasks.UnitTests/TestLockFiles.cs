// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using NuGet.Common;
using NuGet.ProjectModel;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    internal static class TestLockFiles
    {
        public static LockFile GetLockFile(string lockFilePrefix)
        {
            string filePath = Path.Combine("LockFiles", $"{lockFilePrefix}.project.lock.json");

            return LockFileUtilities.GetLockFile(filePath, NullLogger.Instance);
        }

        public static LockFile CreateLockFile(string contents, string path = "path/to/project.lock.json")
        {
            return new LockFileFormat().Parse(contents, path);
        }

        public static string CreateLibrarySnippet(string nameVer, string type = "package", params string [] members)
        {
            return $@" ""{nameVer}"": {{
                ""sha512"": ""abcde=="",
                ""type"": ""{type}"",
                ""files"": [{ToStringList(members)}]
            }}";
        }

        public static string CreateTargetLibrarySnippet(
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
                parts.Add($"\"frameworkAssemblies\": [{string.Join(",", frameworkAssemblies)}]");
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

        private static string ToStringList(params string[] members)
        {
            return members == null ? string.Empty : string.Join(",", $"\"{members}\"");
        }

        public static string TargetLibrarySnippet { get; } = @"
{
  ""locked"": false,
  ""version"": 2,
  ""targets"": {},
  ""libraries"": {},
  ""projectFileDependencyGroups"": {}
}
";
    }
}