// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

internal class ToolPackageUninstaller : IToolPackageUninstaller
{
    private readonly IToolPackageStore _toolPackageStoreQuery;

    public ToolPackageUninstaller(IToolPackageStore toolPackageStoreQuery)
    {
        _toolPackageStoreQuery = toolPackageStoreQuery ?? throw new ArgumentException(nameof(toolPackageStoreQuery));
    }

    public void Uninstall(DirectoryPath packageDirectory)
    {
        var rootDirectory = packageDirectory.GetParentPath();
        string tempPackageDirectory = null;

        TransactionalAction.Run(
            action: () =>
            {
                try
                {
                    if (Directory.Exists(packageDirectory.Value))
                    {
                        // Use the staging directory for uninstall
                        // This prevents cross-device moves when temp is mounted to a different device
                        var tempPath = _toolPackageStoreQuery.GetRandomStagingDirectory().Value;
                        FileAccessRetrier.RetryOnMoveAccessFailure(() =>
                            Directory.Move(packageDirectory.Value, tempPath));
                        tempPackageDirectory = tempPath;
                    }

                    if (Directory.Exists(rootDirectory.Value) &&
                        !Directory.EnumerateFileSystemEntries(rootDirectory.Value).Any())
                    {
                        Directory.Delete(rootDirectory.Value, false);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    throw new ToolPackageException(ex.Message, ex);
                }
            },
            commit: () =>
            {
                if (tempPackageDirectory != null)
                {
                    Directory.Delete(tempPackageDirectory, true);
                }
            },
            rollback: () =>
            {
                if (tempPackageDirectory != null)
                {
                    Directory.CreateDirectory(rootDirectory.Value);
                    FileAccessRetrier.RetryOnMoveAccessFailure(() =>
                        Directory.Move(tempPackageDirectory, packageDirectory.Value));
                }
            });
    }
}
