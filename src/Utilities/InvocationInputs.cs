// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Describes the ordered input files of a cached invocation.
/// </summary>
[Experimental("MSBUILD001")]
public sealed class InvocationInputs
{
    internal string[] Files { get; }

    private InvocationInputs(string[] files)
    {
        Files = files;
    }

    /// <summary>
    /// Describes input files whose absolute paths, contents, and absence contribute to the key.
    /// </summary>
    /// <param name="files">
    /// Ordered input paths, including relevant toolchain and task assemblies.
    /// Relative paths are resolved against the invocation's effective working directory.
    /// </param>
    /// <returns>The input description.</returns>
    public static InvocationInputs FromFiles(params string[] files)
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
                throw new ArgumentException("Input file paths must not be null, empty, or whitespace.", nameof(files));
            }

            copy[i] = file;
        }

        return new InvocationInputs(copy);
    }

    internal InvocationInputs ResolvePaths(AbsolutePath baseDirectory)
    {
        string[] resolved = new string[Files.Length];
        for (int i = 0; i < Files.Length; i++)
        {
            resolved[i] = new AbsolutePath(Files[i], baseDirectory).GetCanonicalForm().Value;
        }

        return new InvocationInputs(resolved);
    }
}
