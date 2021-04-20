// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal static class CommandAliasAssigner
    {
        internal static bool TryAssignAliasesForParameter(Func<string, bool> isAliasTaken, string parameterName, string longNameOverride, string shortNameOverride, out IReadOnlyList<string> assignedAliases)
        {
            List<string> aliasAssignments = new List<string>();
            HashSet<string> invalidParams = new HashSet<string>();

            if (parameterName.IndexOf(':') >= 0)
            {   // Colon is reserved, template param names cannot have any.
                invalidParams.Add(parameterName);
                assignedAliases = aliasAssignments;
                return false;
            }

            string flagFullText = longNameOverride ?? parameterName;
            bool longNameFound = false;
            bool shortNameFound = false;

            // always unless taken
            string nameAsParameter = "--" + flagFullText;
            if (!isAliasTaken(nameAsParameter))
            {
                aliasAssignments.Add(nameAsParameter);
                longNameFound = true;
            }

            // only as fallback
            string qualifiedName = "--param:" + flagFullText;
            if (!longNameFound && !isAliasTaken(qualifiedName))
            {
                aliasAssignments.Add(qualifiedName);
                longNameFound = true;
            }

            if (shortNameOverride == string.Empty)
            {   // it was explicitly empty string in the host file. If it wasn't specified, it'll be null
                shortNameFound = true;
            }
            else if (shortNameOverride != null)
            {
                if (!string.IsNullOrEmpty(shortNameOverride))
                {   // short name starting point was explicitly specified
                    string fullShortNameOverride = "-" + shortNameOverride;

                    if (!isAliasTaken(shortNameOverride))
                    {
                        aliasAssignments.Add(fullShortNameOverride);
                        shortNameFound = true;
                    }

                    string qualifiedShortNameOverride = "-p:" + shortNameOverride;
                    if (!shortNameFound && !isAliasTaken(qualifiedShortNameOverride))
                    {
                        aliasAssignments.Add(qualifiedShortNameOverride);
                        shortNameFound = true;
                    }
                }
            }
            else
            {   // no explicit short name specification, try generating one
                // always unless taken
                string shortName = GetFreeShortName(isAliasTaken, flagFullText);
                if (!isAliasTaken(shortName))
                {
                    aliasAssignments.Add(shortName);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedShortName = GetFreeShortName(isAliasTaken, flagFullText, "p:");
                if (!shortNameFound && !isAliasTaken(qualifiedShortName))
                {
                    aliasAssignments.Add(qualifiedShortName);
                    shortNameFound = true;
                }
            }

            assignedAliases = aliasAssignments;
            return assignedAliases.Count > 0;
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
