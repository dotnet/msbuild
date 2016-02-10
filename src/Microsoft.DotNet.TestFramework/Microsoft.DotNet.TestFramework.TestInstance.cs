// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Microsoft.DotNet.TestFramework
{
    public class TestInstance
    {
        private string _testDestination;
        private TestScenario _testScenario;

        internal TestInstance(TestScenario testScenario, string testDestination)
        {
            if (testScenario == null)
            {
                throw new ArgumentNullException("testScenario");
            }

            if (string.IsNullOrEmpty(testDestination))
            {
                throw new ArgumentException("testDestination");
            }

            _testScenario = testScenario;
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
            var sourceDirs = Directory.GetDirectories(_testScenario.SourceRoot, "*", SearchOption.AllDirectories)
                                 .Where(dir =>
                                 {
                                     dir = dir.ToLower();
                                     return !dir.EndsWith("\\bin") && !dir.Contains("\\bin\\")
                                            && !dir.EndsWith("\\obj") && !dir.Contains("\\obj\\");
                                 });

            foreach (string sourceDir in sourceDirs)
            {
                Directory.CreateDirectory(sourceDir.Replace(_testScenario.SourceRoot, _testDestination));
            }

            var sourceFiles = Directory.GetFiles(_testScenario.SourceRoot, "*.*", SearchOption.AllDirectories)
                                  .Where(file =>
                                  {
                                      file = file.ToLower();
                                      return !file.EndsWith("project.lock.json")
                                            && !file.Contains("\\bin\\") && !file.Contains("\\obj\\");
                                  });

            foreach (string srcFile in sourceFiles)
            {
                File.Copy(srcFile, srcFile.Replace(_testScenario.SourceRoot, _testDestination), true);
            }
        }

        public TestInstance WithLockFiles()
        {
            foreach (string lockFile in Directory.GetFiles(_testScenario.SourceRoot, "project.lock.json", SearchOption.AllDirectories))
            {
                string destinationLockFile = lockFile.Replace(_testScenario.SourceRoot, _testDestination);
                File.Copy(lockFile, destinationLockFile, true);
            }

            return this;
        }

        public TestInstance WithBinaries()
        {
            var binDirs = Directory.GetDirectories(_testScenario.SourceRoot, "*", SearchOption.AllDirectories)
                                 .Where(dir =>
                                 {
                                     dir = dir.ToLower();
                                     return dir.EndsWith("\\bin") || dir.Contains("\\bin\\")
                                            || dir.EndsWith("\\obj") || dir.Contains("\\obj\\");
                                 });

            foreach (string dirPath in binDirs)
            {
                Directory.CreateDirectory(dirPath.Replace(_testScenario.SourceRoot, _testDestination));
            }

            var binFiles = Directory.GetFiles(_testScenario.SourceRoot, "*.*", SearchOption.AllDirectories)
                                 .Where(file =>
                                 {
                                     file = file.ToLower();
                                     return file.Contains("\\bin\\") || file.Contains("\\obj\\");
                                 });

            foreach (string binFile in binFiles)
            {
                File.Copy(binFile, binFile.Replace(_testScenario.SourceRoot, _testDestination), true);
            }

            return this;
        }

        public string TestRoot
        {
            get { return _testDestination; }
        }
    }
}
