// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Newtonsoft.Json.Linq;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.TestFramework
{
    public class TestAsset : TestDirectory
    {
        private readonly string _testAssetRoot;
        private readonly string _buildVersion;

        public string TestRoot => Path;

        internal TestAsset(string testAssetRoot, string testDestination, string buildVersion) : base(testDestination)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            _testAssetRoot = testAssetRoot;
            _buildVersion = buildVersion;
        }

        public TestAsset WithSource()
        {
            var sourceDirs = Directory.GetDirectories(_testAssetRoot, "*", SearchOption.AllDirectories)
              .Where(dir => !IsBinOrObjFolder(dir));

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(_testAssetRoot, Path));
            }

            var sourceFiles = Directory.GetFiles(_testAssetRoot, "*.*", SearchOption.AllDirectories)
                                  .Where(file =>
                                  {
                                      return !IsLockFile(file) && !IsInBinOrObjFolder(file);
                                  });

            foreach (string srcFile in sourceFiles)
            {
                string destFile = srcFile.Replace(_testAssetRoot, Path);
                // For project.json, we need to replace the version of the Microsoft.DotNet.Core.Sdk with the actual build version
                if (System.IO.Path.GetFileName(srcFile).Equals("project.json"))
                {
                    var projectJson = JObject.Parse(File.ReadAllText(srcFile));
                    var sdkDepNodes = GetDependencies(projectJson, "Microsoft.NETCore.Sdk");
                    foreach (var dep in sdkDepNodes)
                    {
                        dep.Value = _buildVersion;
                    }

                    File.WriteAllText(destFile, projectJson.ToString());
                }
                else
                {
                    File.Copy(srcFile, destFile, true);
                }
            }

            return this;
        }

        // Temporary until PackageReferences are listed in the .csproj
        public TestAsset AsSelfContained()
        {
            string projectJsonFilePath = System.IO.Path.Combine(TestRoot, "project.json");

            var projectJson = JObject.Parse(File.ReadAllText(projectJsonFilePath));

            RemoveTypePlatform(projectJson);
            AddCurrentRuntime(projectJson);

            File.WriteAllText(projectJsonFilePath, projectJson.ToString());

            return this;
        }

        private static void AddCurrentRuntime(JObject projectJson)
        {
            var runtimesObject =
                new JObject(
                    new JProperty(RuntimeEnvironment.GetRuntimeIdentifier(), new JObject())
                );

            projectJson.Add("runtimes", runtimesObject);
        }

        private static void RemoveTypePlatform(JObject projectJson)
        {
            var netCoreAppDep = GetDependencies(projectJson, "Microsoft.NETCore.App").FirstOrDefault()?.Value as JObject;
            if (netCoreAppDep != null && netCoreAppDep["type"]?.Value<string>() == "platform")
            {
                netCoreAppDep.Remove("type");
            }
        }

        public TestAsset Restore(string relativePath = "", params string[] args)
        {
            var commandResult = new RestoreCommand(Stage0MSBuild, System.IO.Path.Combine(TestRoot, relativePath))
                .AddSourcesFromCurrentConfig()
                .AddSource(RepoInfo.PackagesPath)
                .Execute(args);

            commandResult.Should().Pass();

            return this;
        }

        private bool IsLockFile(string file)
        {
            file = file.ToLower();
            return file.EndsWith("project.lock.json");
        }

        private bool IsBinOrObjFolder(string directory)
        {
            var binFolder = $"{System.IO.Path.DirectorySeparatorChar}bin";
            var objFolder = $"{System.IO.Path.DirectorySeparatorChar}obj";

            directory = directory.ToLower();
            return directory.EndsWith(binFolder)
                  || directory.EndsWith(objFolder)
                  || IsInBinOrObjFolder(directory);
        }

        private bool IsInBinOrObjFolder(string path)
        {
            var objFolderWithTrailingSlash =
              $"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}";
            var binFolderWithTrailingSlash =
              $"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}";

            path = path.ToLower();
            return path.Contains(binFolderWithTrailingSlash)
                  || path.Contains(objFolderWithTrailingSlash);
        }

        private static IEnumerable<JProperty> GetDependencies(JObject projectJson, string dependencyName)
        {
            return projectJson
                .Descendants()
                .OfType<JProperty>()
                .Where(p => p.Name.Equals("dependencies"))
                .Descendants()
                .OfType<JProperty>()
                .Where(p => p.Name.Equals(dependencyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
