// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
