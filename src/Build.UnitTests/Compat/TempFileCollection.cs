// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Minimal implementation of TempFileCollection

using System;
using System.Collections.Generic;
using System.IO;

namespace System.CodeDom.Compiler
{
    class TempFileCollection : IDisposable
    {
        private Dictionary<string, bool> _files = new Dictionary<string, bool>();

        public void AddFile(string fileName, bool keepFile)
        {
            _files.Add(fileName, keepFile);
        }

        public void Dispose()
        {
            foreach (var f in _files)
            {
                if (f.Value)
                {
                    try
                    {
                        File.Delete(f.Key);
                    }
                    catch
                    {      // ignore
                    }
                }
            }
        }
    }
}
