// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.TemplateEngine.Cli.Alias
{
    internal class AliasManipulationResult
    {
        internal AliasManipulationResult(AliasManipulationStatus status)
            : this(status, null, null)
        {
        }

        internal AliasManipulationResult(AliasManipulationStatus status, string? aliasName, IReadOnlyList<string>? aliasTokens)
        {
            Status = status;
            AliasName = aliasName;
            AliasTokens = aliasTokens ?? Array.Empty<string>();
        }

        internal AliasManipulationStatus Status { get; }

        internal string? AliasName { get; }

        internal IReadOnlyList<string> AliasTokens { get; }
    }
}
