// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.NET.TestFramework
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

            EnsureExistsAndEmpty(Path);
        }

        public string Path { get; private set; }

        private static void EnsureExistsAndEmpty(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch (IOException ex)
                {
                    throw new IOException("Unable to delete directory " + path, ex);
                }
            }

            Directory.CreateDirectory(path);
        }
    }
}
