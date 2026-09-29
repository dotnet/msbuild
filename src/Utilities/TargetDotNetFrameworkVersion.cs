// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Utilities;

/// <summary>
/// Used to specify the targeted version of the .NET Framework for some methods of ToolLocationHelper.
/// </summary>
public enum TargetDotNetFrameworkVersion
{
    /// <summary>
    /// version 1.1
    /// </summary>
    Version11 = 0,

    /// <summary>
    /// version 2.0
    /// </summary>
    Version20 = 1,

    /// <summary>
    /// version 3.0
    /// </summary>
    Version30 = 2,

    /// <summary>
    /// version 3.5
    /// </summary>
    Version35 = 3,

    /// <summary>
    /// version 4.0
    /// </summary>
    Version40 = 4,

    /// <summary>
    /// version 4.5
    /// </summary>
    Version45 = 5,

    /// <summary>
    /// version 4.5.1
    /// </summary>
    Version451 = 6,

    /// <summary>
    /// version 4.6
    /// </summary>
    Version46 = 7,

    /// <summary>
    /// version 4.6.1
    /// </summary>
    Version461 = 8,

    /// <summary>
    /// version 4.5.2. Enum is out of order because it was shipped out of band from a Visual Studio update
    /// without a corresponding SDK release.
    /// </summary>
    Version452 = 9,

    /// <summary>
    /// version 4.6.2
    /// </summary>
    Version462 = 10,

    /// <summary>
    /// version 4.7
    /// </summary>
    Version47 = 11,

    /// <summary>
    /// version 4.7.1
    /// </summary>
    Version471 = 12,

    /// <summary>
    /// version 4.7.2
    /// </summary>
    Version472 = 13,

    /// <summary>
    /// version 4.8
    /// </summary>
    Version48 = 14,

    /// <summary>
    /// version 4.8.1
    /// </summary>
    Version481 = 15,

    /// <summary>
    /// The latest version available at the time of major release. This
    /// value should not be updated in minor releases as it could be a
    /// breaking change. Use 'Latest' if possible, but note the
    /// compatibility implications.
    /// </summary>
    VersionLatest = Version48,

    /// <summary>
    /// Sentinel value for the latest version that this version of MSBuild is aware of. Similar
    /// to VersionLatest except the compiled value in the calling application will not need to
    /// change for the update in MSBuild to be used.
    /// </summary>
    /// <remarks>
    /// This value was introduced in Visual Studio 15.1. It is incompatible with previous
    /// versions of MSBuild.
    /// </remarks>
    Latest = 9999
}
