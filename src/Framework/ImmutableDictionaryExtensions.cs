// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Framework
{
    internal static class ImmutableDictionaryExtensions
    {
        /// <summary>
        /// An empty dictionary pre-configured with a comparer for metadata dictionaries.
        /// </summary>
        public static readonly ImmutableDictionary<string, string> EmptyMetadata =
            ImmutableDictionary<string, string>.Empty.WithComparers(MSBuildNameIgnoreCaseComparer.Default);

        /// <summary>
        /// Sets the given items while running a validation function on each key.
        /// </summary>
        /// <remarks>
        /// ProjectItemInstance.TaskItem exposes dictionary values as ProjectMetadataInstance. For perf reasons,
        /// we don't want to internally store ProjectMetadataInstance since it prevents us from sharing immutable
        /// dictionaries with Utilities.TaskItem, and it results in more than 2x memory allocated per-entry.
        /// </remarks>
        public static ImmutableDictionary<string, string> SetItems(
            this ImmutableDictionary<string, string> dictionary,
            IEnumerable<KeyValuePair<string, string>> items,
            Action<string> verifyThrowKey)
        {
            return dictionary.SetItems(ValidateItems(items, verifyThrowKey));

            // PERF: This extra allocation is still faster than enumerating ImmutableDictionary after modification.
            static IEnumerable<KeyValuePair<string, string>> ValidateItems(
                IEnumerable<KeyValuePair<string, string>> items,
                Action<string> verifyThrowKey)
            {
                foreach (KeyValuePair<string, string> item in items)
                {
                    verifyThrowKey(item.Key);

                    // Set null as empty string to match behavior with ProjectMetadataInstance.
                    yield return item.Value == null
                        ? new KeyValuePair<string, string>(item.Key, string.Empty)
                        : item;
                }
            }
        }
    }
}
