// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Shared;

internal static class MSBuildArchitecture
{
    public const string x86 = "x86";
    public const string x64 = "x64";
    public const string arm64 = "arm64";
    public const string currentArchitecture = "CurrentArchitecture";
    public const string any = "*";

    /// <summary>
    /// Returns the MSBuildArchitecture value corresponding to the current process' architecture.
    /// </summary>
    /// <comments>
    /// Revisit if we ever run on something other than Intel.
    /// </comments>
    public static string GetCurrent()
        => NativeMethodsShared.Is64Bit ? x64 : x86;
}
