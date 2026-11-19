// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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
    public static InvocationInputs FromFiles(params string[] files) =>
        new InvocationInputs(Array.ConvertAll(files, Path.GetFullPath));
}
