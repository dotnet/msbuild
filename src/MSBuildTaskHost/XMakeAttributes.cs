// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Contains the names of the known attributes in the XML project file.
    /// </summary>
    internal static class XMakeAttributes
    {
        internal struct MSBuildRuntimeValues
        {
            internal const string clr2 = "CLR2";
            internal const string clr4 = "CLR4";
            internal const string currentRuntime = "CurrentRuntime";
            internal const string net = "NET";
            internal const string any = "*";
        }

        internal struct MSBuildArchitectureValues
        {
            internal const string x86 = "x86";
            internal const string x64 = "x64";
            internal const string arm64 = "arm64";
            internal const string currentArchitecture = "CurrentArchitecture";
            internal const string any = "*";
        }

        /// <summary>
        /// Returns the MSBuildArchitecture value corresponding to the current process' architecture.
        /// </summary>
        /// <comments>
        /// Revisit if we ever run on something other than Intel.
        /// </comments>
        internal static string GetCurrentMSBuildArchitecture()
        {
            string currentArchitecture = (IntPtr.Size == sizeof(Int64)) ? MSBuildArchitectureValues.x64 : MSBuildArchitectureValues.x86;
            return currentArchitecture;
        }
    }
}
