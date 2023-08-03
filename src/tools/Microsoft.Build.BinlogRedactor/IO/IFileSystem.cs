// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BinlogRedactor.IO;
internal interface IFileSystem
{
    void CreateDirectory(string path);

    bool FileExists(string path);

    bool DirectoryExists(string path);

    bool PathExists(string path);

    void RenameFile(string original, string @new);
    void ReplaceFile(string source, string destination);
    long GetFileSizeInBytes(string path);

    void DeleteDirectory(string path);

    IEnumerable<string> EnumerateDirectories(string dir);
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions);

    void FileCopy(string sourceFileName, string destFileName, bool overwrite);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions);
    StreamWriter CreateFileStream(string path, bool append);
}
