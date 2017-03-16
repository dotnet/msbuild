// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks
{
    class ConflictResolver
    {
        private Dictionary<string, ConflictItem> winningItemsByKey = new Dictionary<string, ConflictItem>();
        private ILog log;
        private PackageRank packageRank;

        public ConflictResolver(PackageRank packageRank, ILog log)
        {
            this.log = log;
            this.packageRank = packageRank;
        }

        public void ResolveConflicts(IEnumerable<ConflictItem> conflictItems, Func<ConflictItem, string> getItemKey, Action<ConflictItem> foundConflict, bool commitWinner = true)
        {
            if (conflictItems == null)
            {
                return;
            }

            foreach (var conflictItem in conflictItems)
            {
                var itemKey = getItemKey(conflictItem);

                if (String.IsNullOrEmpty(itemKey))
                {
                    continue;
                }

                ConflictItem existingItem;

                if (winningItemsByKey.TryGetValue(itemKey, out existingItem))
                {
                    // a conflict was found, determine the winner.
                    var winner = ResolveConflict(existingItem, conflictItem);

                    if (winner == null)
                    {
                        // no winner, skip it.
                        // don't add to conflict list and just use the existing item for future conflicts.
                        continue;
                    }

                    ConflictItem loser = conflictItem;
                    if (!ReferenceEquals(winner, existingItem))
                    {
                        // replace existing item
                        if (commitWinner)
                        {
                            winningItemsByKey[itemKey] = conflictItem;
                        }
                        else
                        {
                            winningItemsByKey.Remove(itemKey);
                        }
                        loser = existingItem;

                    }

                    foundConflict(loser);
                }
                else if (commitWinner)
                {
                    winningItemsByKey[itemKey] = conflictItem;
                }
            }
        }

        private ConflictItem ResolveConflict(ConflictItem item1, ConflictItem item2)
        {
            var conflictMessage = $"Encountered conflict between {item1.DisplayName} and {item2.DisplayName}.";

            var exists1 = item1.Exists;
            var exists2 = item2.Exists;

            if (!exists1 || !exists2)
            {
                var fileMessage = !exists1 ?
                                    !exists2 ?
                                      "both files do" :
                                      $"{item1.DisplayName} does" :
                                  $"{item2.DisplayName} does";

                log.LogMessage($"{conflictMessage}.  Could not determine winner because {fileMessage} not exist.");
                return null;
            }

            var assemblyVersion1 = item1.AssemblyVersion;
            var assemblyVersion2 = item2.AssemblyVersion;

            // if only one is missing version stop: something is wrong when we have a conflict between assembly and non-assembly
            if (assemblyVersion1 == null ^ assemblyVersion2 == null)
            {
                var nonAssembly = assemblyVersion1 == null ? item1.DisplayName : item2.DisplayName;
                log.LogMessage($"{conflictMessage}. Could not determine a winner because {nonAssembly} is not an assembly.");
                return null;
            }

            // only handle cases where assembly version is different, and not null (implicit here due to xor above)
            if (assemblyVersion1 != assemblyVersion2)
            {
                if (assemblyVersion1 > assemblyVersion2)
                {
                    log.LogMessage($"{conflictMessage}.  Choosing {item1.DisplayName} because AssemblyVersion {assemblyVersion1} is greater than {assemblyVersion2}.");
                    return item1;
                }

                if (assemblyVersion2 > assemblyVersion1)
                {
                    log.LogMessage($"{conflictMessage}.  Choosing {item2.DisplayName} because AssemblyVersion {assemblyVersion2} is greater than {assemblyVersion1}.");
                    return item2;
                }
            }

            var fileVersion1 = item1.FileVersion;
            var fileVersion2 = item2.FileVersion;

            // if only one is missing version
            if (fileVersion1 == null ^ fileVersion2 == null)
            {
                var nonVersion = fileVersion1 == null ? item1.DisplayName : item2.DisplayName;
                log.LogMessage($"{conflictMessage}. Could not determine a winner because {nonVersion} has no file version.");
                return null;
            }

            if (fileVersion1 != fileVersion2)
            {
                if (fileVersion1 > fileVersion2)
                {
                    log.LogMessage($"{conflictMessage}.  Choosing {item1.DisplayName} because file version {fileVersion1} is greater than {fileVersion2}.");
                    return item1;
                }

                if (fileVersion2 > fileVersion1)
                {
                    log.LogMessage($"{conflictMessage}.  Choosing {item2.DisplayName} because file version {fileVersion2} is greater than {fileVersion1}.");
                    return item2;
                }
            }

            var packageRank1 = packageRank.GetPackageRank(item1.PackageId);
            var packageRank2 = packageRank.GetPackageRank(item2.PackageId);

            if (packageRank1 < packageRank2)
            {
                log.LogMessage($"{conflictMessage}.  Choosing {item1.DisplayName} because package it comes from a package that is preferred.");
                return item1;
            }

            if (packageRank2 < packageRank1)
            {
                log.LogMessage($"{conflictMessage}.  Choosing {item2.DisplayName} because package it comes from a package that is preferred.");
                return item2;
            }

            var isPlatform1 = item1.ItemType == ConflictItemType.Platform;
            var isPlatform2 = item2.ItemType == ConflictItemType.Platform;

            if (isPlatform1 && !isPlatform2)
            {
                log.LogMessage($"{conflictMessage}.  Choosing {item1.DisplayName} because it is a platform item.");
                return item1;
            }

            if (!isPlatform1 && isPlatform2)
            {
                log.LogMessage($"{conflictMessage}.  Choosing {item2.DisplayName} because it is a platform item.");
                return item1;
            }

            log.LogMessage($"{conflictMessage}.  Could not determine winner due to equal file and assembly versions.");
            return null;
        }
    }
}
