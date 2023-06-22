// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    /// <summary>
    /// This class is doing assignments of aliases to template options based on their settings and other options already defined in the command.
    /// </summary>
    internal class AliasAssignmentCoordinator
    {
        internal static IReadOnlyList<(CliTemplateParameter Parameter, IReadOnlySet<string> Aliases, IReadOnlyList<string> Errors)> AssignAliasesForParameter(IEnumerable<CliTemplateParameter> parameters, HashSet<string> takenAliases)
        {
            List<(CliTemplateParameter Parameter, IReadOnlySet<string> Aliases, IReadOnlyList<string> Errors)> result = new();

            List<string> predefinedLongOverrides = parameters.SelectMany(p => p.LongNameOverrides).Where(n => !string.IsNullOrEmpty(n)).Select(n => $"--{n}").ToList();
            List<string> predefinedShortOverrides = parameters.SelectMany(p => p.ShortNameOverrides).Where(n => !string.IsNullOrEmpty(n)).Select(n => $"-{n}").ToList();

            Func<string, bool> isAliasTaken = takenAliases.Contains;
            Func<string, bool> isLongNamePredefined = predefinedLongOverrides.Contains;
            Func<string, bool> isShortNamePredefined = predefinedShortOverrides.Contains;

            foreach (var parameter in parameters)
            {
                HashSet<string> aliases = new HashSet<string>(StringComparer.Ordinal);
                List<string> errors = new List<string>();
                if (parameter.Name.Contains(':'))
                {
                    // Colon is reserved, template param names cannot have any.
                    errors.Add(string.Format(LocalizableStrings.AliasAssignmentCoordinator_Error_NameShouldNotHaveColon, parameter.Name));
                    result.Add((parameter, aliases, errors));
                    continue;
                }

                HandleLongOverrides(takenAliases, aliases, errors, isAliasTaken, isLongNamePredefined, parameter);
                HandleShortOverrides(takenAliases, aliases, errors, isAliasTaken, parameter);

                //if there is already short name override defined, do not generate new one
                if (parameter.ShortNameOverrides.Any())
                {
                    result.Add((parameter, aliases, errors));
                    continue;
                }

                GenerateShortName(takenAliases, aliases, errors, isAliasTaken, isShortNamePredefined, parameter);
                result.Add((parameter, aliases, errors));
            }
            return result;
        }

        private static void HandleShortOverrides(
            HashSet<string> takenAliases,
            HashSet<string> aliases,
            List<string> errors,
            Func<string, bool> isAliasTaken,
            CliTemplateParameter parameter)
        {
            foreach (string shortNameOverride in parameter.ShortNameOverrides)
            {
                if (shortNameOverride == string.Empty)
                {
                    // it was explicitly empty string in the host file.
                    continue;
                }
                if (!string.IsNullOrEmpty(shortNameOverride))
                {
                    // short name starting point was explicitly specified
                    string fullShortNameOverride = "-" + shortNameOverride;
                    if (!isAliasTaken(fullShortNameOverride))
                    {
                        aliases.Add(fullShortNameOverride);
                        takenAliases.Add(fullShortNameOverride);
                        continue;
                    }

                    //if taken, we append prefix
                    string qualifiedShortNameOverride = "-p:" + shortNameOverride;
                    if (!isAliasTaken(qualifiedShortNameOverride))
                    {
                        aliases.Add(qualifiedShortNameOverride);
                        takenAliases.Add(qualifiedShortNameOverride);
                        continue;
                    }
                    errors.Add(string.Format(LocalizableStrings.AliasAssignmentCoordinator_Error_ShortAlias, parameter.Name, fullShortNameOverride, qualifiedShortNameOverride));
                }
            }
        }

        private static void HandleLongOverrides(
            HashSet<string> takenAliases,
            HashSet<string> aliases,
            List<string> errors,
            Func<string, bool> isAliasTaken,
            Func<string, bool> isLongNamePredefined,
            CliTemplateParameter parameter)
        {
            bool noLongOverrideDefined = false;
            IEnumerable<string> longNameOverrides = parameter.LongNameOverrides;

            //if no long override define, we use parameter name
            if (!longNameOverrides.Any())
            {
                longNameOverrides = new[] { parameter.Name };
                noLongOverrideDefined = true;
            }

            foreach (string longName in longNameOverrides)
            {
                string optionName = "--" + longName;
                if ((!noLongOverrideDefined && !isAliasTaken(optionName))
                    //if we use parameter name, we should also check if there is any other parameter which defines this long name.
                    //in case it is, then we should give precedence to other parameter to use it.
                    || (noLongOverrideDefined && !isAliasTaken(optionName) && !isLongNamePredefined(optionName)))
                {
                    aliases.Add(optionName);
                    takenAliases.Add(optionName);
                    continue;
                }

                // if parameter name is taken
                optionName = "--param:" + longName;
                if (!isAliasTaken(optionName))
                {
                    aliases.Add(optionName);
                    takenAliases.Add(optionName);
                    continue;
                }
                errors.Add(string.Format(LocalizableStrings.AliasAssignmentCoordinator_Error_LongAlias, parameter.Name, $"--{longName}", $"--param:{longName}"));
            }
        }

        private static void GenerateShortName(
            HashSet<string> takenAliases,
            HashSet<string> aliases,
            List<string> errors,
            Func<string, bool> isAliasTaken,
            Func<string, bool> isShortNamePredefined,
            CliTemplateParameter parameter)
        {
            //use long override as base, if exists
            string flagFullText = parameter.LongNameOverrides.Count > 0 ? parameter.LongNameOverrides[0] : parameter.Name;

            // try to generate un-prefixed name, if not taken.
            string shortName = GetFreeShortName(s => isAliasTaken(s) || (isShortNamePredefined(s)), flagFullText);
            if (!isAliasTaken(shortName))
            {
                aliases.Add(shortName);
                takenAliases.Add(shortName);
                return;
            }

            // try to generate prefixed name, as the fallback
            string qualifiedShortName = GetFreeShortName(s => isAliasTaken(s) || (isShortNamePredefined(s)), flagFullText, "p:");
            if (!isAliasTaken(qualifiedShortName))
            {
                aliases.Add(qualifiedShortName);
                takenAliases.Add(qualifiedShortName);
                return;
            }
            Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.AliasAssignmentCoordinator_Error_ShortAlias, parameter.Name, shortName, qualifiedShortName));
        }

        private static string GetFreeShortName(Func<string, bool> isAliasTaken, string name, string prefix = "")
        {
            string[] parts = name.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            string[] buckets = new string[parts.Length];

            for (int i = 0; i < buckets.Length; ++i)
            {
                buckets[i] = parts[i].Substring(0, 1);
            }

            int lastBucket = parts.Length - 1;
            while (isAliasTaken("-" + prefix + string.Join("", buckets)))
            {
                //Find the next thing we can take a character from
                bool first = true;
                int end = (lastBucket + 1) % parts.Length;
                int i = (lastBucket + 1) % parts.Length;
                for (; first || i != end; first = false, i = (i + 1) % parts.Length)
                {
                    if (parts[i].Length > buckets[i].Length)
                    {
                        buckets[i] = parts[i].Substring(0, buckets[i].Length + 1);
                        break;
                    }
                }

                if (i == end)
                {
                    break;
                }
            }

            return "-" + prefix + string.Join("", buckets);
        }
    }
}
