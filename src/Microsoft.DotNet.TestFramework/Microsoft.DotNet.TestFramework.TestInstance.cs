// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.TestFramework
{
    public class TestInstance
    {
        private string _testDestination;
        private string _testAssetRoot;

        internal TestInstance(string testAssetRoot, string testDestination)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testScenario");
            }

            if (string.IsNullOrEmpty(testDestination))
            {
                throw new ArgumentException("testDestination");
            }

            _testAssetRoot = testAssetRoot;
            _testDestination = testDestination;

            if (Directory.Exists(testDestination))
            {
                Directory.Delete(testDestination, true);
            }

            Directory.CreateDirectory(testDestination);
            CopySource();
        }

        private void CopySource()
        {
            var sourceDirs = Directory.GetDirectories(_testAssetRoot, "*", SearchOption.AllDirectories)
                                 .Where(dir =>
                                 {
                                     dir = dir.ToLower();
                                     return !dir.EndsWith($"{Path.DirectorySeparatorChar}bin")
                                            && !dir.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                            && !dir.EndsWith($"{Path.DirectorySeparatorChar}obj")
                                            && !dir.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
                                 });

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(_testAssetRoot, _testDestination));
            }

            var sourceFiles = Directory.GetFiles(_testAssetRoot, "*.*", SearchOption.AllDirectories)
                                  .Where(file =>
                                  {
                                      file = file.ToLower();
                                      return !file.EndsWith("project.lock.json")
                                            && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") 
                                            && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
                                  });

            foreach (string srcFile in sourceFiles)
            {
                string destFile = srcFile.Replace(_testAssetRoot, _testDestination);
                File.Copy(srcFile, destFile, true);
                FixTimeStamp(srcFile, destFile);
            }
        }

        public TestInstance WithLockFiles()
        {
            foreach (string lockFile in Directory.GetFiles(_testAssetRoot, "project.lock.json", SearchOption.AllDirectories))
            {
                string destinationLockFile = lockFile.Replace(_testAssetRoot, _testDestination);
                File.Copy(lockFile, destinationLockFile, true);
                FixTimeStamp(lockFile, destinationLockFile);
            }

            return this;
        }

        public TestInstance WithBuildArtifacts()
        {
            var binDirs = Directory.GetDirectories(_testAssetRoot, "*", SearchOption.AllDirectories)
                                 .Where(dir =>
                                 {
                                     dir = dir.ToLower();
                                     return dir.EndsWith($"{Path.DirectorySeparatorChar}bin") 
                                            || dir.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                            || dir.EndsWith($"{Path.DirectorySeparatorChar}obj") 
                                            || dir.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
                                 });

            foreach (string dirPath in binDirs)
            {
                Directory.CreateDirectory(dirPath.Replace(_testAssetRoot, _testDestination));
            }

            var binFiles = Directory.GetFiles(_testAssetRoot, "*.*", SearchOption.AllDirectories)
                                 .Where(file =>
                                 {
                                     file = file.ToLower();
                                     return file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") 
                                            || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
                                 });

            foreach (string binFile in binFiles)
            {
                string destFile = binFile.Replace(_testAssetRoot, _testDestination);
                File.Copy(binFile, destFile, true);
                FixTimeStamp(binFile, destFile);
            }

            return this;
        }

        public string TestRoot
        {
            get { return _testDestination; }
        }

        private static void FixTimeStamp(string originalFile, string newFile)
        {
            // workaround for https://github.com/dotnet/corefx/issues/6083
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var originalTime = File.GetLastWriteTime(originalFile);
                File.SetLastWriteTime(newFile, originalTime);
            }
        }
    }
}
