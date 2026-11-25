// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
    /// <param name="path">The owned directory, resolved against the current directory.</param>
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

        return new InvocationOutputs(Path.GetFullPath(path), Array.Empty<string>());
    }

    /// <summary>
    /// Describes a directory replaced in its entirety on a cache hit,
    /// resolving a relative path against the specified task environment.
    /// </summary>
    /// <param name="path">The owned directory, resolved against <paramref name="environment"/>.</param>
    /// <param name="environment">The task environment used to resolve a relative path.</param>
    /// <returns>The owned-directory description.</returns>
    public static InvocationOutputs FromDirectory(string path, TaskEnvironment environment)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Output directory path must not be empty or whitespace.", nameof(path));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        return new InvocationOutputs(environment.GetAbsolutePath(path).GetCanonicalForm().Value, Array.Empty<string>());
    }

    /// <summary>
    /// Describes ordered output files, including files that may be absent.
    /// </summary>
    /// <param name="files">The output paths, resolved against the current directory.</param>
    /// <returns>The file-set description; unrelated files are not changed on a cache hit.</returns>
    public static InvocationOutputs FromFiles(params string[] files)
    {
        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }

        string[] resolved = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentException("Output file paths must not be null, empty, or whitespace.", nameof(files));
            }

            resolved[i] = Path.GetFullPath(file);
        }

        return new InvocationOutputs(null, resolved);
    }

    /// <summary>
    /// Describes ordered output files, including files that may be absent,
    /// resolving relative paths against the specified task environment.
    /// </summary>
    /// <param name="files">The output paths, resolved against <paramref name="environment"/>.</param>
    /// <param name="environment">The task environment used to resolve relative paths.</param>
    /// <returns>The file-set description; unrelated files are not changed on a cache hit.</returns>
    public static InvocationOutputs FromFiles(IEnumerable<string> files, TaskEnvironment environment)
    {
        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        var resolved = new List<string>();
        foreach (string file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentException("Output file paths must not be null, empty, or whitespace.", nameof(files));
            }

            resolved.Add(environment.GetAbsolutePath(file).GetCanonicalForm().Value);
        }

        return new InvocationOutputs(null, resolved.ToArray());
    }
}
