// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Logging;

public sealed class ArchiveFileEventArgs : EventArgs
{
    private ArchiveFile  _archiveFile;
    private bool _resultSet;
    private Action _disposeAction;

    public ArchiveFileEventArgs(ArchiveFile archiveFile) =>
        (_archiveFile, _resultSet, _disposeAction) = (archiveFile, true, archiveFile.Dispose);

    /// <summary>
    /// Acquires the <see cref="ArchiveFile"/> instance. This method can only be called once and
    /// <see cref="SetResult(string,Stream)"/> or <see cref="SetResult(string,string)"/> must be called afterwards
    /// (this is because the embedded files are stored as forward only stream - reading them prevents re-reads).
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public ArchiveFile ObtainArchiveFile()
    {
        if (!_resultSet)
        {
            throw new InvalidOperationException(
                "ArchiveFile was obtained, but the final edited version was not set.");
        }

        _resultSet = false;
        return _archiveFile;
    }

    public void SetResult(string resultPath, Stream resultStream)
    {
        _archiveFile = new ArchiveFile(resultPath, resultStream);
        _disposeAction += _archiveFile.Dispose;
        _resultSet = true;
    }

    public void SetResult(string resultPath, string resultContent)
    {
        _archiveFile = new ArchiveFile(resultPath, resultContent);
        _disposeAction += _archiveFile.Dispose;
        _resultSet = true;
    }

    // Intentionally not exposing this publicly (e.g. as IDisposable implementation)
    // as we don't want to user to be bothered with ownership and disposing concerns.
    internal void Dispose() => _disposeAction();
}
