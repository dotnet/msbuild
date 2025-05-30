// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
namespace System.Runtime.Versioning
{
    /// <summary>
    /// SupportedOSPlatform is a net5.0+ Attribute.
    /// Create the same type only in full-framework and netstandard2.0 builds
    /// to prevent many #if RUNTIME_TYPE_NETCORE checks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    internal class SupportedOSPlatformGuard : Attribute
    {
        internal SupportedOSPlatformGuard(string platformName)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class)]
    internal class SupportedOSPlatform : Attribute
    {
        internal SupportedOSPlatform(string platformName)
        {
        }
    }
}
#endif
