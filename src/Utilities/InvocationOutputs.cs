// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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
    public static InvocationOutputs FromDirectory(string path) =>
        new InvocationOutputs(Path.GetFullPath(path), Array.Empty<string>());

    /// <summary>
    /// Describes ordered output files, including files that may be absent.
    /// </summary>
    /// <param name="files">The output paths, resolved against the current directory.</param>
    /// <returns>The file-set description; unrelated files are not changed on a cache hit.</returns>
    public static InvocationOutputs FromFiles(params string[] files) =>
        new InvocationOutputs(null, Array.ConvertAll(files, Path.GetFullPath));
}
