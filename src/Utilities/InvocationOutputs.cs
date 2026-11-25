// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Describes an invocation's owned directory or ordered set of output files.
/// </summary>
[Experimental("MSBUILD001")]
public sealed class InvocationOutputs
{
    internal string? DirectoryPath { get; }
    internal string[] Files { get; }

    private InvocationOutputs(string? directoryPath, string[] files)
    {
        DirectoryPath = directoryPath;
        Files = files;
    }

    /// <summary>
    /// Describes a directory replaced in its entirety on a cache hit.
    /// </summary>
    /// <param name="path">
    /// The owned directory. Relative paths are resolved against the invocation's effective working directory.
    /// </param>
    /// <returns>The owned-directory description.</returns>
    public static InvocationOutputs FromDirectory(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Output directory path must not be empty or whitespace.", nameof(path));
        }

        return new InvocationOutputs(path, Array.Empty<string>());
    }

    /// <summary>
    /// Describes ordered output files, including files that may be absent.
    /// </summary>
    /// <param name="files">
    /// The output paths. Relative paths are resolved against the invocation's effective working directory.
    /// </param>
    /// <returns>The file-set description; unrelated files are not changed on a cache hit.</returns>
    public static InvocationOutputs FromFiles(params string[] files)
    {
        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }

        string[] copy = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentException("Output file paths must not be null, empty, or whitespace.", nameof(files));
            }

            copy[i] = file;
        }

        return new InvocationOutputs(null, copy);
    }

    internal InvocationOutputs ResolvePaths(AbsolutePath baseDirectory)
    {
        if (DirectoryPath is not null)
        {
            return new InvocationOutputs(
                new AbsolutePath(DirectoryPath, baseDirectory).GetCanonicalForm().Value,
                Array.Empty<string>());
        }

        string[] resolved = new string[Files.Length];
        for (int i = 0; i < Files.Length; i++)
        {
            resolved[i] = new AbsolutePath(Files[i], baseDirectory).GetCanonicalForm().Value;
        }

        return new InvocationOutputs(null, resolved);
    }
}
