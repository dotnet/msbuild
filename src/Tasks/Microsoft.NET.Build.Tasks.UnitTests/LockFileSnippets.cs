// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    internal static class LockFileSnippets
    {
        #region LockFile String Snippet Creation Utilities

        public static string CreateLockFileSnippet(
            string[] targets,
            string[] libraries,
            string[] projectFileDependencyGroups,
            string[] logs = null)
        {
            return $@"{{
              ""version"": 3,
              ""targets"": {{{string.Join(",", targets)}}},
              ""libraries"": {{{string.Join(",", libraries)}}},
              {GetLogsPart(logs)}
              ""projectFileDependencyGroups"": {{{string.Join(",", projectFileDependencyGroups)}}},
              {LockFileProjectSection}
            }}";
        }

        public static string CreateCrossTargetingLockFileSnippet(
            string[] targets,
            string[] originalTargetFrameworks,
            string[] targetFrameworks,
            string[] libraries = null,
            string[] projectFileDependencyGroups = null,
            string[] logs = null)
        {
            return $@"{{
  ""version"": 3,
  ""targets"": {{{string.Join(",", targets)}}},
  ""libraries"": {{{(libraries == null ? string.Empty : string.Join(",", libraries))}}},
  {GetLogsPart(logs)}
  ""projectFileDependencyGroups"": {{{(projectFileDependencyGroups == null ? string.Empty : string.Join(",", projectFileDependencyGroups))}}},
  ""project"": {{
    ""version"": ""1.0.0"",
    ""restore"": {{
      'projectUniqueName': 'C:\\git\\repro\\consoletest\\consoletest.csproj',
      'projectName': 'consoletest',
      'projectPath': 'C:\\git\\repro\\consoletest\\consoletest.csproj',
      ""packagesPath"": ""C:\\Users\\username\\.nuget\\packages\\"",
      ""outputPath"": ""C:\\code\\tmp\\obj\\"",
      ""projectStyle"": ""PackageReference"",
      ""crossTargeting"": true,
      ""fallbackFolders"": [],
      ""configFilePaths"": [
        ""C:\\Users\\username\\AppData\\Roaming\\NuGet\\NuGet.Config""
      ],
      ""originalTargetFrameworks"": [
        {string.Join(",", originalTargetFrameworks)}
      ],
      ""sources"": {{
        ""https://api.nuget.org/v3/index.json"": {{}},
      }},
      ""frameworks"": {{
        {string.Join(",", targetFrameworks)}
      }},
      ""warningProperties"": {{
        ""warnAsError"": [
          ""NU1605""
        ]
      }}
    }},
    ""frameworks"": {{
      {string.Join(",", targetFrameworks)}
    }}
  }}
}}";
        }

        private static string LockFileProjectSection = @"  'project': {
    'version': '1.0.0',
    'restore': {
      'projectUniqueName': 'C:\\git\\repro\\consoletest\\consoletest.csproj',
      'projectName': 'consoletest',
      'projectPath': 'C:\\git\\repro\\consoletest\\consoletest.csproj',
      'packagesPath': 'C:\\Users\\username\\.nuget\\packages\\',
      'outputPath': 'C:\\git\\repro\\consoletest\\obj\\',
      'projectStyle': 'PackageReference',
      'fallbackFolders': [],
      'configFilePaths': [
        'C:\\Users\\username\\AppData\\Roaming\\NuGet\\NuGet.Config'
      ],
      'originalTargetFrameworks': [
        'netcoreapp1.0'
      ],
      'sources': {
        'https://api.nuget.org/v3/index.json': {}
      },
      'frameworks': {
        'netcoreapp1.0': {
          'targetAlias': 'netcoreapp1.0',
          'projectReferences': {}
        }
      },
      'warningProperties': {
        'warnAsError': [
          'NU1605'
        ]
      }
    },
    'frameworks': {
      'netcoreapp1.0': {
        'targetAlias': 'netcoreapp1.0',
        'dependencies': {
          'Microsoft.NETCore.App': {
            'target': 'Package',
            'version': '[1.0.5, )',
            'autoReferenced': true
          }
        },
        'runtimeIdentifierGraphPath': 'C:\\git\\dotnet-sdk\\artifacts\\bin\\redist\\Debug\\dotnet\\sdk\\5.0.100-dev\\RuntimeIdentifierGraph.json'
      }
    }
  }".Replace('\'', '"');

        private static string GetLogsPart(string[] logs)
            => logs == null ? string.Empty : $@" ""logs"": [{string.Join(",", logs)}], ";

        public static string CreateLibrary(string nameVer, string type = "package", params string[] members)
        {
            return $@" ""{nameVer}"": {{
                ""sha512"": ""abcde=="",
                ""type"": ""{type}"",
                ""path"": ""{nameVer}"",
                ""files"": [{ToStringList(members)}]
            }}";
        }

        public static string CreateProjectLibrary(string nameVer, string path, string msbuildProject)
        {
            return $@" ""{nameVer}"": {{
                ""type"": ""project"",
                ""path"": ""{path}"",
                ""msbuildProject"": ""{msbuildProject}""
            }}";
        }

        public static string CreateTarget(string tfm, params string[] targetLibs)
        {
            return $"\"{tfm}\": {{{string.Join(",", targetLibs)}}}";
        }

        public static string CreateTargetFramework(string tfm, string targetAlias = null)
        {
            return @$"""{tfm}"": {{
    ""targetAlias"": ""{(targetAlias == null ? tfm : targetAlias)}""
}}";
        }

        public static string CreateTargetLibrary(
            string nameVer,
            string type = "package",
            string[] dependencies = null,
            string[] frameworkAssemblies = null,
            string[] compile = null,
            string[] runtime = null,
            string[] native = null,
            string[] resource = null,
            string[] runtimeTargets = null,
            string[] contentFiles = null)
        {
            List<string> parts = new();
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
            addListIfPresent("native", native);
            addListIfPresent("resource", resource);
            addListIfPresent("runtimeTargets", runtimeTargets);
            addListIfPresent("contentFiles", contentFiles);

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

        public static string CreateLog(NuGetLogCode code, LogLevel level, string message,
            string filePath = null,
            string libraryId = null,
            string warningLevel = "0",
            string[] targetGraphs = null)
        {
            List<string> parts = new();

            parts.Add($"\"code\": \"{code}\"");
            parts.Add($"\"level\": \"{level}\"");
            parts.Add($"\"message\": \"{message}\"");
            parts.Add($"\"warningLevel\": \"{warningLevel}\"");

            if (filePath != null) parts.Add($"\"filePath\": \"{filePath}\"");
            if (libraryId != null) parts.Add($"\"libraryId\": \"{libraryId}\"");
            if (targetGraphs != null) parts.Add($"\"targetGraphs\": [{ToStringList(targetGraphs)}]");

            return $@"{{
                {string.Join(",", parts)}
            }}";
        }

        private static string ToStringList(params string[] members)
        {
            return members == null ? string.Empty : string.Join(",", members.Select(m => $"\"{m}\""));
        }

        #endregion

        #region Default Lock File String Snippets

        public static readonly string ProjectGroup =
            CreateProjectFileDependencyGroup("", "LibA >= 1.2.3");

        public static readonly string NETCoreGroup =
            CreateProjectFileDependencyGroup(".NETCoreApp,Version=v1.0");

        public static readonly string NETCoreOsxGroup =
            CreateProjectFileDependencyGroup(".NETCoreApp,Version=v1.0/osx.10.11-x64");

        public static readonly string net462Group =
            CreateProjectFileDependencyGroup(".NETFramework,Version=v4.6.2");

        public static readonly string LibADefn =
            CreateLibrary("LibA/1.2.3", "package", "lib/file/A.dll", "lib/file/B.dll", "lib/file/C.dll");

        public static readonly string LibBDefn =
            CreateLibrary("LibB/1.2.3", "package", "lib/file/D.dll", "lib/file/E.dll", "lib/file/F.dll");

        public static readonly string LibCDefn =
            CreateLibrary("LibC/1.2.3", "package", "lib/file/G.dll", "lib/file/H.dll", "lib/file/I.dll");

        public static readonly string TargetLibA = CreateTargetLibrary("LibA/1.2.3", "package",
            dependencies: new string[] { "\"LibB\": \"1.2.3\"" },
            frameworkAssemblies: new string[] { "System.Some.Lib" },
            compile: new string[] { CreateFileItem("lib/file/A.dll"), CreateFileItem("lib/file/B.dll") },
            runtime: new string[] { CreateFileItem("lib/file/A.dll"), CreateFileItem("lib/file/B.dll") }
            );

        public static readonly string TargetLibB = CreateTargetLibrary("LibB/1.2.3", "package",
            dependencies: new string[] { "\"LibC\": \"1.2.3\"" },
            frameworkAssemblies: new string[] { "System.Some.Lib" },
            compile: new string[] { CreateFileItem("lib/file/D.dll"), CreateFileItem("lib/file/E.dll") },
            runtime: new string[] { CreateFileItem("lib/file/D.dll"), CreateFileItem("lib/file/E.dll") }
            );

        public static readonly string TargetLibC = CreateTargetLibrary("LibC/1.2.3", "package",
            compile: new string[] { CreateFileItem("lib/file/G.dll"), CreateFileItem("lib/file/H.dll") },
            runtime: new string[] { CreateFileItem("lib/file/G.dll"), CreateFileItem("lib/file/H.dll") }
            );

        public static readonly string TargetLibBAllAssets = CreateTargetLibrary("LibB/1.2.3", "package",
            frameworkAssemblies: new string[] { "System.Some.Lib" },
            dependencies: new string[] { "\"LibC\": \"1.2.3\"" },
            compile: new string[] { CreateFileItem("lib/file/C1.dll") },
            runtime: new string[] { CreateFileItem("lib/file/R1.dll") },
            native: new string[] { CreateFileItem("lib/file/N1.dll") },
            resource: new string[] {
                    CreateFileItem(
                        "lib/file/R2.resources.dll",
                        metadata: new Dictionary<string, string>() { {"locale", "de"} })
            },
            runtimeTargets: new string[] {
                    CreateFileItem(
                        "runtimes/osx/native/R3.dylib",
                        metadata: new Dictionary<string, string>() {
                            { "assetType", "native"}, { "rid", "osx" }
                        })
            },
            contentFiles: new string[] {
                    CreateFileItem(
                        "contentFiles/any/images/C2.png",
                        metadata: new Dictionary<string, string>() {
                            { "buildAction", "EmbeddedResource" },
                            { "codeLanguage", "any" },
                            { "copyToOutput", "false" },
                        })
            }
            );

        #endregion
    }
}
