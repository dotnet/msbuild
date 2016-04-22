// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.TestFramework
{
    public class TestInstance: TestDirectory
    {
        private string _testAssetRoot;

        internal TestInstance(string testAssetRoot, string testDestination) : base(testDestination)
        {
            if (string.IsNullOrEmpty(testAssetRoot))
            {
                throw new ArgumentException("testAssetRoot");
            }

            _testAssetRoot = testAssetRoot;
            
            CopySource();
        }

        private void CopySource()
        {
            var sourceDirs = Directory.GetDirectories(_testAssetRoot, "*", SearchOption.AllDirectories)
                                 .Where(dir =>
                                 {
                                     dir = dir.ToLower();
                                     return !dir.EndsWith($"{System.IO.Path.DirectorySeparatorChar}bin")
                                            && !dir.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}")
                                            && !dir.EndsWith($"{System.IO.Path.DirectorySeparatorChar}obj")
                                            && !dir.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}");
                                 });

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(_testAssetRoot, Path));
            }

            var sourceFiles = Directory.GetFiles(_testAssetRoot, "*.*", SearchOption.AllDirectories)
                                  .Where(file =>
                                  {
                                      file = file.ToLower();
                                      return !file.EndsWith("project.lock.json")
                                            && !file.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}") 
                                            && !file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}");
                                  });

            foreach (string srcFile in sourceFiles)
            {
                string destFile = srcFile.Replace(_testAssetRoot, Path);
                File.Copy(srcFile, destFile, true);
                FixTimeStamp(srcFile, destFile);
            }
        }

        public TestInstance WithLockFiles()
        {
            foreach (string lockFile in Directory.GetFiles(_testAssetRoot, "project.lock.json", SearchOption.AllDirectories))
            {
                string destinationLockFile = lockFile.Replace(_testAssetRoot, Path);
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
                                     return dir.EndsWith($"{System.IO.Path.DirectorySeparatorChar}bin") 
                                            || dir.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}")
                                            || dir.EndsWith($"{System.IO.Path.DirectorySeparatorChar}obj") 
                                            || dir.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}");
                                 });

            foreach (string dirPath in binDirs)
            {
                Directory.CreateDirectory(dirPath.Replace(_testAssetRoot, Path));
            }

            var binFiles = Directory.GetFiles(_testAssetRoot, "*.*", SearchOption.AllDirectories)
                                 .Where(file =>
                                 {
                                     file = file.ToLower();
                                     return file.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}") 
                                            || file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}");
                                 });

            foreach (string binFile in binFiles)
            {
                string destFile = binFile.Replace(_testAssetRoot, Path);
                File.Copy(binFile, destFile, true);
                FixTimeStamp(binFile, destFile);
            }

            return this;
        }

        public string TestRoot => Path;

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
