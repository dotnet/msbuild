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
        private readonly string _path;

        internal TestDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path");
            }
            
            _path = path;
            
            EnsureExistsAndEmpty(_path);
        }
        
        public string Path { get { return _path; } }
        
        private void EnsureExistsAndEmpty(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }
    }
}