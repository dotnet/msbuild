// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Cli.Alias
{
    internal class AliasModel
    {
        internal AliasModel()
            : this(new Dictionary<string, IReadOnlyList<string>>())
        {
        }

        internal AliasModel(IReadOnlyDictionary<string, IReadOnlyList<string>> commandAliases)
        {
            CommandAliases = new Dictionary<string, IReadOnlyList<string>>(commandAliases.ToDictionary(x => x.Key, x => x.Value), StringComparer.OrdinalIgnoreCase);
        }

        [JsonProperty]
        internal Dictionary<string, IReadOnlyList<string>> CommandAliases { get; set; }

        internal void AddCommandAlias(string aliasName, IReadOnlyList<string> aliasTokens)
        {
            CommandAliases.Add(aliasName, aliasTokens);
        }

        internal bool TryRemoveCommandAlias(string aliasName, out IReadOnlyList<string>? aliasTokens)
        {
            if (CommandAliases.TryGetValue(aliasName, out aliasTokens))
            {
                CommandAliases.Remove(aliasName);
                return true;
            }

            return false;
        }
    }
}
