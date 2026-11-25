// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
    /// <param name="files">Ordered input paths, including relevant toolchain and task assemblies.</param>
    /// <returns>The input description, with paths resolved against the current directory.</returns>
    public static InvocationInputs FromFiles(params string[] files)
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
                throw new ArgumentException("Input file paths must not be null, empty, or whitespace.", nameof(files));
            }

            resolved[i] = Path.GetFullPath(file);
        }

        return new InvocationInputs(resolved);
    }

    /// <summary>
    /// Describes input files whose absolute paths, contents, and absence contribute to the key,
    /// resolving relative paths against the specified task environment.
    /// </summary>
    /// <param name="files">Ordered input paths, including relevant toolchain and task assemblies.</param>
    /// <param name="environment">The task environment used to resolve relative paths.</param>
    /// <returns>The input description, with canonicalized paths resolved against <paramref name="environment"/>.</returns>
    public static InvocationInputs FromFiles(IEnumerable<string> files, TaskEnvironment environment)
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
                throw new ArgumentException("Input file paths must not be null, empty, or whitespace.", nameof(files));
            }

            resolved.Add(environment.GetAbsolutePath(file).GetCanonicalForm().Value);
        }

        return new InvocationInputs(resolved.ToArray());
    }
}
