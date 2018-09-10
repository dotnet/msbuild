// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
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
