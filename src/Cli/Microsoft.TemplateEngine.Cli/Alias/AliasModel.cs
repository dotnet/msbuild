// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
