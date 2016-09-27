// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
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
                                      return !IsInBinOrObjFolder(file);
                                  });

            foreach (string srcFile in sourceFiles)
            {
                string destFile = srcFile.Replace(_testAssetRoot, Path);
                // For project.json, we need to replace the version of the Microsoft.DotNet.Core.Sdk with the actual build version
                if (System.IO.Path.GetFileName(srcFile).EndsWith("proj"))
                {
                    var project = XDocument.Load(srcFile);

                    project
                        .Descendants(XName.Get("PackageReference", "http://schemas.microsoft.com/developer/msbuild/2003"))
                        .FirstOrDefault(pr => pr.Attribute("Include")?.Value == "Microsoft.NETCore.Sdk")
                        ?.Element(XName.Get("Version", "http://schemas.microsoft.com/developer/msbuild/2003"))
                        ?.SetValue(_buildVersion);

                    using (var file = File.CreateText(destFile))
                    {
                        project.Save(file);
                    }
                }
                else
                {
                    File.Copy(srcFile, destFile, true);
                }
            }

            return this;
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
    }
}
