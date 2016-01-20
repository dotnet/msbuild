// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public class DefaultCompilerWarningSuppresses
    {
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Suppresses { get; } = new Dictionary<string, IReadOnlyList<string>>
        {
            { "csc", new string[] {"CS1701", "CS1702", "CS1705" } }
        };
    }
}
