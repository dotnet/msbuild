// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim.Tests
{
    internal class FakeFile : IFile
    {
        private Dictionary<string, string> _files;

        public FakeFile(Dictionary<string, string> files)
        {
            _files = files;
        }

        public bool Exists(string path)
        {
            return _files.ContainsKey(path);
        }

        public string ReadAllText(string path)
        {
            throw new NotImplementedException();
        }

        public Stream OpenRead(string path)
        {
            throw new NotImplementedException();
        }

        public Stream OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare,
            int bufferSize,
            FileOptions fileOptions)
        {
            throw new NotImplementedException();
        }


        public void CreateEmptyFile(string path)
        {
            _files.Add(path, String.Empty);
        }

        public void WriteAllText(string path, string content)
        {
            _files[path] = content;
        }

        public void Delete(string path)
        {
            throw new NotImplementedException();
        }

        public static FakeFile Empty => new FakeFile(new Dictionary<string, string>());
    }
}
