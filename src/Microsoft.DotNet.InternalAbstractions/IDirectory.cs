// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    internal interface IDirectory
    {
        bool Exists(string path);

        ITemporaryDirectory CreateTemporaryDirectory();

        IEnumerable<string> EnumerateFileSystemEntries(string path);

        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern);

        string GetDirectoryFullName(string path);

        void CreateDirectory(string path);

        void Delete(string path, bool recursive);

        void Move(string source, string destination);
    }
}
