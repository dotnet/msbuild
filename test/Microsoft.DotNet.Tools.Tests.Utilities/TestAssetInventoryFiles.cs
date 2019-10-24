// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAssetInventoryFiles
    {
        private FileInfo _source;

        private FileInfo _restore;

        private FileInfo _build;

        public FileInfo Source
        {
            get
            {
                _source.Refresh();

                return _source;
            }

            private set
            {
                _source = value;
            }
        }

        public FileInfo Restore
        {
            get
            {
                _restore.Refresh();

                return _restore;
            }

            private set
            {
                _restore = value;
            }
        }

        public FileInfo Build
        {
            get
            {
                _build.Refresh();

                return _build;
            }

            private set
            {
                _build = value;
            }
        }

        public TestAssetInventoryFiles(DirectoryInfo inventoryFileDirectory)
        {
            Source = new FileInfo(Path.Combine(inventoryFileDirectory.FullName, "source.txt"));

            Restore = new FileInfo(Path.Combine(inventoryFileDirectory.FullName, "restore.txt"));

            Build = new FileInfo(Path.Combine(inventoryFileDirectory.FullName, "build.txt"));
        }

        public IEnumerable<FileInfo> AllInventoryFiles
        {
            get
            {
                return new List<FileInfo>
                {
                    Source,
                    Restore,
                    Build
                };
            }
        }
    }
}
