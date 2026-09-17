// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

// This is a supporting forwarder for an internal polyfill API
[assembly: TypeForwardedTo(typeof(OSPlatformAttribute))]

#else

namespace System.Runtime.Versioning;

internal abstract class OSPlatformAttribute(string platformName) : Attribute
{
    public string PlatformName => platformName;
}
#endif
