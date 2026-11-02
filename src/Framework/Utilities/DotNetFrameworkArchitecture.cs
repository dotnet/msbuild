// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Used to specify the targeted bitness of the .NET Framework for some methods of ToolLocationHelper
/// </summary>
public enum DotNetFrameworkArchitecture
{
    /// <summary>
    /// Indicates the .NET Framework that is currently being run under.
    /// </summary>
    Current = 0,

    /// <summary>
    /// Indicates the 32-bit .NET Framework
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Bitness", Justification = "Bitness is a reasonable term")]
    Bitness32 = 1,

    /// <summary>
    /// Indicates the 64-bit .NET Framework
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Bitness", Justification = "Bitness is a reasonable term")]
    Bitness64 = 2,
}
