// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

// This is a supporting forwarder for an internal polyfill API
[assembly: TypeForwardedTo(typeof(SupportedOSPlatformGuardAttribute))]

#else

namespace System.Runtime.Versioning;

[AttributeUsage(
    AttributeTargets.Field |
    AttributeTargets.Method |
    AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = false)]
internal sealed class SupportedOSPlatformGuardAttribute(string platformName) : OSPlatformAttribute(platformName)
{
}
#endif
