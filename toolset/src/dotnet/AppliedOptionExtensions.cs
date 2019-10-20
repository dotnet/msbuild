// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class AppliedOptionExtensions
    {
        public static T ValueOrDefault<T>(this AppliedOption parseResult, string alias)
        {
            return parseResult
                .AppliedOptions
                .Where(o => o.HasAlias(alias))
                .Select(o => o.Value<T>())
                .SingleOrDefault();
        }

        public static string SingleArgumentOrDefault(this AppliedOption parseResult, string alias)
        {
            return parseResult
                .AppliedOptions
                .Where(o => o.HasAlias(alias))
                .Select(o => o.Arguments.Single())
                .SingleOrDefault();
        }

        public static bool IsHelpRequested(this AppliedOption appliedOption)
        {
            return appliedOption.HasOption("help") ||
                   appliedOption.Arguments.Contains("-?") ||
                   appliedOption.Arguments.Contains("/?");
        }
    }
}
