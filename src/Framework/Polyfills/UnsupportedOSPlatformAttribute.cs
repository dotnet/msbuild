// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

// This is a supporting forwarder for an internal polyfill API
[assembly: TypeForwardedTo(typeof(UnsupportedOSPlatformAttribute))]

#else

namespace System.Runtime.Versioning;

[AttributeUsage(
    AttributeTargets.Assembly |
    AttributeTargets.Class |
    AttributeTargets.Constructor |
    AttributeTargets.Enum |
    AttributeTargets.Event |
    AttributeTargets.Field |
    AttributeTargets.Interface |
    AttributeTargets.Method |
    AttributeTargets.Module |
    AttributeTargets.Property |
    AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false)]
internal sealed class UnsupportedOSPlatformAttribute : OSPlatformAttribute
{
    public string? Message { get; }

    public UnsupportedOSPlatformAttribute(string platformName, string? message)
        : base(platformName)
    {
        Message = message;
    }

    public UnsupportedOSPlatformAttribute(string platformName)
        : base(platformName)
    {
    }
}
#endif
