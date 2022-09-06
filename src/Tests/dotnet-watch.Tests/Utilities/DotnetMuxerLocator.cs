// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.Extensions.Tools.Internal;

/// <summary>
/// Locates SDK to be used for running tests.
/// </summary>
internal static class DotnetMuxerLocator
{
    public static string MuxerPath;

    static DotnetMuxerLocator()
    {
        MuxerPath = GetDotnetMuxerPath();
    }

    private static string GetDotnetMuxerPath()
    {
        var muxerFileName = "dotnet" + Constants.ExeSuffix;

        var candidateDirs = GetLookupDirectories().ToArray();

        foreach (var candidateDir in candidateDirs)
        {
            var muxerPath = Path.Combine(candidateDir, muxerFileName);
            if (File.Exists(muxerPath))
            {
                return muxerPath;
            }
        }

        throw new InvalidOperationException($"Unable to locate dotnet. Tried: '{string.Join("', '", candidateDirs)}'.");
    }

    private static IEnumerable<string> GetLookupDirectories()
    {
        var installDir = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");
        if (installDir != null)
        {
            yield return installDir;
        }

        var paths = Environment.GetEnvironmentVariable("PATH");
        if (paths != null)
        {
            foreach (var path in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return path;
            }
        }
    }
}
