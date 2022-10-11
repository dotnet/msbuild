// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        public static Option<bool> CreateDiagnosticsOption() => new(
            new string[] { "-d", "--diagnostics" },
            Microsoft.DotNet.Tools.Help.LocalizableStrings.SDKDiagnosticsCommandDefinition);
    }
}
