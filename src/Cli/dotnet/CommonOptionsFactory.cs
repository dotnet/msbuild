// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    /// <summary>
    /// Creates common options.
    /// </summary>
    internal static class CommonOptionsFactory
    {
        /// <summary>
        /// Creates common diagnositcs option (-d|--diagnostics).
        /// </summary>
        public static CliOption<bool> CreateDiagnosticsOption(bool recursive) => new("--diagnostics", "-d")
        {
            Description = Microsoft.DotNet.Tools.Help.LocalizableStrings.SDKDiagnosticsCommandDefinition,
            Recursive = recursive
        };
    }
}
