// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Utilities;

/// <summary>
/// Used to specify the version of Visual Studio from which to select associated
/// tools for some methods of ToolLocationHelper
/// </summary>
public enum VisualStudioVersion
{
    /// <summary>
    /// Visual Studio 2010 (Dev10) and SP1
    /// </summary>
    Version100,

    /// <summary>
    /// Visual Studio 2012 (Dev11)
    /// </summary>
    Version110,

    /// <summary>
    /// Visual Studio 2013 (Dev12)
    /// </summary>
    Version120,

    /// <summary>
    /// Visual Studio 2015 (Dev14)
    /// </summary>
    Version140,

    /// <summary>
    /// Visual Studio 2017 (Dev15)
    /// </summary>
    Version150,

    /// <summary>
    /// Visual Studio 2019 (Dev16)
    /// </summary>
    Version160,

    /// <summary>
    /// Visual Studio 2022 (Dev17)
    /// </summary>
    Version170,

    /// <summary>
    /// Dev18
    /// </summary>
    Version180,

    // keep this up-to-date; always point to the last entry.
    /// <summary>
    /// The latest version available at the time of release
    /// </summary>
    VersionLatest = Version180,
}
