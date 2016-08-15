// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.TestFramework.Assertions;
using Microsoft.DotNet.TestFramework.Commands;
using static Microsoft.DotNet.TestFramework.Commands.MSBuildTest;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAsset : TestDirectory
    {
        // made tolower because the rest of the class works with normalized tolower strings
        private static readonly IEnumerable<string> BuildArtifactBlackList = new List<string>()
        {
          ".IncrementalCache",
          ".SDKVersion"
        }.Select(s => s.ToLower()).ToArray();

        private string _testAssetRoot;

        public string TestRoot => Path;

        internal TestAsset(string testAssetRoot, string testDestination) : base(testDestination)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            _testAssetRoot = testAssetRoot;
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
                File.Copy(srcFile, destFile, true);
            }

            return this;
        }

        // This is temporary, we won't need it once the rest of the targets are updated to use
        // project.assets.json
        public TestAsset WithLockFile()
        {
            var sourceProjectLockJson = System.IO.Path.Combine(_testAssetRoot, "project.lock.json");
            var targetProjectLockJson = sourceProjectLockJson.Replace(_testAssetRoot, Path);
            File.Copy(sourceProjectLockJson, targetProjectLockJson);

            return this;
        }

        public TestAsset Restore(params string[] args)
        {
            var restoreCommand = new RestoreCommand(Stage0MSBuild, TestRoot);
            var commandResult = restoreCommand.Execute(args);

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
    }
}
