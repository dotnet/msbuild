// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
