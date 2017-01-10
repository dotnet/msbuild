// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.TestFramework
{
    public class TestDirectory
    {
        internal TestDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(nameof(path));
            }
            
            Path = path;
            
            EnsureDirectoryAndBackupDirectoryExistAndAreEmpty(Path);
        }

        public string Path { get; private set; }

        private static void EnsureDirectoryAndBackupDirectoryExistAndAreEmpty(string path)
        {
            var testDirectory = new DirectoryInfo(path);

            var migrationBackupDirectory = new DirectoryInfo(
                System.IO.Path.Combine(testDirectory.Parent.FullName, "backup"));

            if (testDirectory.Exists)
            {
                testDirectory.Delete(true);
            } 
            
            if (migrationBackupDirectory.Exists)
            {
                migrationBackupDirectory.Delete(true);
            }

            Directory.CreateDirectory(path);
        }
    }
}
