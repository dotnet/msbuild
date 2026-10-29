// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

/// <summary>
/// Describes the ordered input files used to compute a <see cref="PersistentCache"/> key.
/// </summary>
[Experimental("MSBUILD001")]
public sealed class PersistentCacheInputs
{
    internal string[] Files { get; }

    private PersistentCacheInputs(string[] files) => Files = files;

    /// <summary>
    /// Describes input files whose paths, contents, and absence contribute to the key.
    /// </summary>
    /// <param name="files">
    /// Ordered file paths, resolved relative to the current directory. Include toolchain
    /// files and task assemblies that can affect the outputs.
    /// </param>
    /// <returns>The input description, independent of subsequent changes to the supplied array.</returns>
    public static PersistentCacheInputs FromFiles(params string[] files)
    {
        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }

        return new PersistentCacheInputs(Array.ConvertAll(files, path =>
        {
            PersistentCacheOutputs.ValidatePath(path);
            return Path.GetFullPath(path);
        }));
    }
}
