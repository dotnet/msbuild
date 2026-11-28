// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

/// <summary>
///  A deterministic file system that reports the specified files, their containing directories,
///  and the specified directories as existing.
/// </summary>
internal sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _files;
    private readonly HashSet<string> _directories;

    /// <summary>
    ///  Initializes a file system with the specified files and directories.
    /// </summary>
    /// <param name="files">The full paths of files that exist.</param>
    /// <param name="directories">The full paths of directories that exist.</param>
    public FakeFileSystem(IEnumerable<string>? files = null, IEnumerable<string>? directories = null)
    {
        _files = new(files ?? [], FileUtilities.PathComparer);
        _directories = new(directories ?? [], FileUtilities.PathComparer);

        foreach (string file in _files)
        {
            if (Path.GetDirectoryName(file) is { Length: > 0 } directory)
            {
                _directories.Add(directory);
            }
        }
    }

    public bool DirectoryExists(string path)
        => _directories.Contains(path);

    public bool FileExists(string path)
        => _files.Contains(path);

    public bool FileOrDirectoryExists(string path)
        => FileExists(path) || DirectoryExists(path);

    public TextReader ReadFile(string path)
        => throw new NotSupportedException();

    public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        => throw new NotSupportedException();

    public string ReadFileAllText(string path)
        => throw new NotSupportedException();

    public byte[] ReadFileAllBytes(string path)
        => throw new NotSupportedException();

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => throw new NotSupportedException();

    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => throw new NotSupportedException();

    public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => throw new NotSupportedException();

    public FileAttributes GetAttributes(string path)
        => throw new NotSupportedException();

    public DateTime GetLastWriteTimeUtc(string path)
        => throw new NotSupportedException();
}
