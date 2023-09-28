// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.Alias
{
    internal class AliasRegistry
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly string _aliasesFilePath;
        private AliasModel? _aliases;

        internal AliasRegistry(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _aliasesFilePath = Path.Combine(_environmentSettings.Paths.HostVersionSettingsDir, "aliases.json");
        }

        internal IReadOnlyDictionary<string, IReadOnlyList<string>> AllAliases
        {
            get
            {
                EnsureLoaded();
                //ensure loaded sets aliases
                return new Dictionary<string, IReadOnlyList<string>>(_aliases!.CommandAliases, StringComparer.OrdinalIgnoreCase);
            }
        }

        internal AliasManipulationResult TryCreateOrRemoveAlias(string aliasName, IReadOnlyList<string> aliasTokens)
        {
            EnsureLoaded();

            if (aliasName == null)
            {
                // the input was malformed. Alias flag without alias name
                return new AliasManipulationResult(AliasManipulationStatus.InvalidInput);
            }
            else if (aliasTokens.Count == 0)
            {
                // the command was just "--alias <alias name>"
                // remove the alias
                //ensure loaded sets aliases
                if (_aliases!.TryRemoveCommandAlias(aliasName, out IReadOnlyList<string>? removedAliasTokens))
                {
                    Save();
                    return new AliasManipulationResult(AliasManipulationStatus.Removed, aliasName, removedAliasTokens);
                }
                else
                {
                    return new AliasManipulationResult(AliasManipulationStatus.RemoveNonExistentFailed, aliasName, null);
                }
            }

            //ensure loaded sets aliases
            Dictionary<string, IReadOnlyList<string>> aliasesWithCandidate = new(_aliases!.CommandAliases);
            aliasesWithCandidate[aliasName] = aliasTokens;
            if (!TryExpandCommandAliases(aliasesWithCandidate, aliasTokens, out IReadOnlyList<string>? expandedInputTokens))
            {
                return new AliasManipulationResult(AliasManipulationStatus.WouldCreateCycle, aliasName, aliasTokens);
            }

            _aliases.AddCommandAlias(aliasName, aliasTokens);
            Save();
            return new AliasManipulationResult(AliasManipulationStatus.Created, aliasName, aliasTokens);
        }

        // Attempts to expand aliases on the input string, using the aliases in _aliases
        internal bool TryExpandCommandAliases(IReadOnlyList<string> inputTokens, out IReadOnlyList<string> expandedInputTokens)
        {
            EnsureLoaded();

            if (inputTokens.Count == 0)
            {
                expandedInputTokens = new List<string>(inputTokens);
                return true;
            }

            //ensure loaded sets aliases
            if (TryExpandCommandAliases(_aliases!.CommandAliases, inputTokens, out expandedInputTokens!))
            {
                return true;
            }

            // TryExpandCommandAliases() returned false because was an expansion error
            expandedInputTokens = new List<string>();
            return false;
        }

        private static bool TryExpandCommandAliases(IReadOnlyDictionary<string, IReadOnlyList<string>> aliases, IReadOnlyList<string> inputTokens, out IReadOnlyList<string>? expandedTokens)
        {
            bool expansionOccurred = false;
            HashSet<string> seenAliases = new();
            expandedTokens = new List<string>(inputTokens);

            do
            {
                string candidateAliasName = expandedTokens[0];

                if (aliases.TryGetValue(candidateAliasName, out IReadOnlyList<string>? aliasExpansion))
                {
                    if (!seenAliases.Add(candidateAliasName))
                    {
                        // a cycle has occurred.... not allowed.
                        expandedTokens = null;
                        return false;
                    }

                    // The expansion is the combination of the aliasExpansion (expands the 0th token of the previously expandedTokens)
                    //  and the rest of the previously expandedTokens
                    expandedTokens = new CombinedList<string>(aliasExpansion, expandedTokens.ToList().GetRange(1, expandedTokens.Count - 1));
                    expansionOccurred = true;
                }
                else
                {
                    expansionOccurred = false;
                }
            }
            while (expansionOccurred);

            return true;
        }

        private void EnsureLoaded()
        {
            if (_aliases != null)
            {
                return;
            }

            if (!_environmentSettings.Host.FileSystem.FileExists(_aliasesFilePath))
            {
                _aliases = new AliasModel();
                return;
            }
            JObject parsed = _environmentSettings.Host.FileSystem.ReadObject(_aliasesFilePath);
            IReadOnlyDictionary<string, IReadOnlyList<string>> commandAliases = ToStringListDictionary(parsed, StringComparer.OrdinalIgnoreCase, "CommandAliases");

            _aliases = new AliasModel(commandAliases);
        }

        private void Save()
        {
            if (_aliases is AliasModel { CommandAliases: { Count: > 0 } })
            {
                _environmentSettings.Host.FileSystem.WriteObject(_aliasesFilePath, _aliases);
            }
            else
            {
                _environmentSettings.Host.FileSystem.FileDelete(_aliasesFilePath);
            }
        }

        // reads a dictionary whose values can either be string literals, or arrays of strings.
        private IReadOnlyDictionary<string, IReadOnlyList<string>> ToStringListDictionary(JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, IReadOnlyList<string>> result = new(comparer ?? StringComparer.Ordinal);
            JObject? jObj = token as JObject;
            if (jObj == null || propertyName == null || !jObj.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out JToken? element))
            {
                return result;
            }

            jObj = element as JObject;
            if (jObj == null)
            {
                return result;
            }

            foreach (JProperty property in jObj.Properties())
            {
                if (property.Value == null)
                {
                    continue;
                }
                else if (property.Value.Type == JTokenType.String)
                {
                    result[property.Name] = new List<string>() { property.Value.ToString() };
                }
                else if (property.Value.Type == JTokenType.Array)
                {
                    JArray? arr = property.Value as JArray;
                    if (arr == null)
                    {
                        result[property.Name] = Array.Empty<string>();
                    }
                    else
                    {
                        List<string> values = new();
                        foreach (JToken item in arr)
                        {
                            if (item != null && item.Type == JTokenType.String)
                            {
                                values.Add(item.ToString());
                            }
                        }
                        result[property.Name] = values;
                    }
                }
            }

            return result;
        }
    }
}
