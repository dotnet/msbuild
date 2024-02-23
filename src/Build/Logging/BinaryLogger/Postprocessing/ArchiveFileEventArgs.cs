// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

/// <summary>
/// Event arguments for <see cref="IBuildEventArgsReaderNotifications.ArchiveFileEncountered"/> event.
/// </summary>
public sealed class ArchiveFileEventArgs : EventArgs
{
    public ArchiveData ArchiveData { get; set; }

    public ArchiveFileEventArgs(ArchiveData archiveData) => ArchiveData = archiveData;

    // Intentionally not exposing this publicly (e.g. as IDisposable implementation)
    // as we don't want to user to be bothered with ownership and disposing concerns.
    internal void Dispose() => ArchiveData.Dispose();
}
