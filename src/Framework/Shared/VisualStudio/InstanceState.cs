// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP && FEATURE_VISUALSTUDIOSETUP

using System;

namespace Microsoft.Build.Shared.VisualStudio;

/// <summary>
///  Bitfield describing how complete the installer considers a given Visual Studio
///  instance. Only an instance whose state equals <see cref="Complete"/> (all known
///  state bits set) is safe for tooling to invoke against.
/// </summary>
/// <remarks>
///  Declared in <c>Setup.Configuration.idl</c> as <c>InstanceState : DWORD</c>.
/// </remarks>
[Flags]
internal enum InstanceState : uint
{
    /// <summary>No state set yet; the install record is still being populated.</summary>
    None = 0,

    /// <summary>The instance's files exist on disk under its installation path.</summary>
    Local = 1,

    /// <summary>The instance is registered with the OS (Add/Remove Programs, COM, etc.).</summary>
    Registered = 2,

    /// <summary>The instance does not need a reboot before it can be used.</summary>
    NoRebootRequired = 4,

    /// <summary>The instance has no recorded installer errors.</summary>
    NoErrors = 8,

    /// <summary>
    ///  Sentinel "everything is set" value (<c>MAXUINT</c> in the IDL). Matches the
    ///  <c>InstanceState.Complete</c> constant in the managed
    ///  <c>Microsoft.VisualStudio.Setup.Configuration.Interop</c> RCW wrapper that this
    ///  enum replaces.
    /// </summary>
    Complete = uint.MaxValue,
}

#endif
