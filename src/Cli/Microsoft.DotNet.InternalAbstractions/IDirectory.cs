// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    internal interface IDirectory
    {
        bool Exists(string path);

        ITemporaryDirectory CreateTemporaryDirectory();

        IEnumerable<string> EnumerateDirectories(string path);

        IEnumerable<string> EnumerateFiles(string path);

        IEnumerable<string> EnumerateFileSystemEntries(string path);

        string GetCurrentDirectory();

        void CreateDirectory(string path);

        void Delete(string path, bool recursive);

        void Move(string source, string destination);


        /// <Summary>Returns a new directory created under the temp folder. Can be on the mock under test or the real temp file folder.</Summary>
        string CreateTemporarySubdirectory();
    }
}
