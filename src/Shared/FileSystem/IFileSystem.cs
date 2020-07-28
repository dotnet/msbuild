// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared.FileSystem
{

    /*
     * This is a clone of Microsoft.Build.FileSystem.IMSBuildFileSystem.
     * IMSBuildFileSystem is the reference interface, changes should be made to IMSBuildFileSystem and cloned in IFileSystem.
     * Any new code should depend on IMSBuildFileSystem instead of IFileSystem, if possible.
     *
     * Ideally there should be only one, public interface. However, such an interface would need to be put into the 
     * Microsoft.Build.Framework assembly, but that assembly cannot take new types because it breaks some old version of Nuget.exe.
     */
    internal interface IFileSystem
    {
        TextReader ReadFile(string path);
        Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share);

        string ReadFileAllText(string path);

        byte[] ReadFileAllBytes(string path);

        IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        FileAttributes GetAttributesNoMissingException(string path);

        bool DirectoryExists(string path);

        bool FileExists(string path);

        bool DirectoryEntryExists(string path);

        void ClearCaches();

        void WriteStatistics(TextWriter writer);
    }
}
