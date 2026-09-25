// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Forwards to the evaluation file system and records every path evaluation reads, probes, or enumerates.
/// Reads are recorded before the read so the recorded timestamp cannot postdate the content evaluation consumed.
/// </summary>
internal sealed class RecordingFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly EvaluationInputRecorder _recorder;

    internal RecordingFileSystem(IFileSystem inner, EvaluationInputRecorder recorder)
    {
        _inner = inner;
        _recorder = recorder;
    }

    public TextReader ReadFile(string path)
    {
        _recorder.RecordPath(path);
        return _inner.ReadFile(path);
    }

    public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        _recorder.RecordPath(path);
        return _inner.GetFileStream(path, mode, access, share);
    }

    public string ReadFileAllText(string path)
    {
        _recorder.RecordPath(path);
        return _inner.ReadFileAllText(path);
    }

    public byte[] ReadFileAllBytes(string path)
    {
        _recorder.RecordPath(path);
        return _inner.ReadFileAllBytes(path);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _recorder.RecordPath(path);
        return _inner.EnumerateFiles(path, searchPattern, searchOption);
    }

    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _recorder.RecordPath(path);
        return _inner.EnumerateDirectories(path, searchPattern, searchOption);
    }

    public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _recorder.RecordPath(path);
        return _inner.EnumerateFileSystemEntries(path, searchPattern, searchOption);
    }

    public FileAttributes GetAttributes(string path)
    {
        _recorder.RecordPath(path);
        return _inner.GetAttributes(path);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        _recorder.RecordPath(path);
        return _inner.GetLastWriteTimeUtc(path);
    }

    public bool DirectoryExists(string path)
    {
        bool exists = _inner.DirectoryExists(path);
        _recorder.RecordProbe(path, ProbeKind.Directory, exists);
        return exists;
    }

    public bool FileExists(string path)
    {
        bool exists = _inner.FileExists(path);
        _recorder.RecordProbe(path, ProbeKind.File, exists);
        return exists;
    }

    public bool FileOrDirectoryExists(string path)
    {
        bool exists = _inner.FileOrDirectoryExists(path);
        _recorder.RecordProbe(path, ProbeKind.FileOrDirectory, exists);
        return exists;
    }
}
