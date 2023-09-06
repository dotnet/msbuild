// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers.LocalDaemons;

internal class ArchiveFileRegistry : ILocalRegistry
{
    private readonly string _archiveOutputPath;

    public ArchiveFileRegistry(string archiveOutputPath)
    {
        _archiveOutputPath = archiveOutputPath;
    }

    public async Task LoadAsync(BuiltImage image, SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(_archiveOutputPath);

        // pointing to a directory? -> append default name
        if (Directory.Exists(fullPath) || _archiveOutputPath.EndsWith("/") || _archiveOutputPath.EndsWith("\\"))
        {
            fullPath = Path.Combine(fullPath, destinationReference.Repository + ".tar.gz");
        }

        // create parent directory if required.
        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (parentDirectory != null && !Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        await using var fileStream = File.Create(fullPath);
        await DockerCli.WriteImageToStreamAsync(
            image,
            sourceReference,
            destinationReference,
            fileStream,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public bool IsAvailable() => true;

    public override string ToString() => "Archive File";
}
