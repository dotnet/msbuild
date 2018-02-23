// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    //  The conflict resolver finds conflicting items, and if there are any of them it reports the "losing" item via the foundConflict callback
    internal class ConflictResolver<TConflictItem> where TConflictItem : class, IConflictItem
    {
        private Dictionary<string, TConflictItem> winningItemsByKey = new Dictionary<string, TConflictItem>();
        private ILog log;
        private PackageRank packageRank;

        public ConflictResolver(PackageRank packageRank, ILog log)
        {
            this.log = log;
            this.packageRank = packageRank;
        }

        public void ResolveConflicts(IEnumerable<TConflictItem> conflictItems, Func<TConflictItem, string> getItemKey,
            Action<TConflictItem> foundConflict, bool commitWinner = true,
            Action<TConflictItem> unresolvedConflict = null)
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

                TConflictItem existingItem;

                if (winningItemsByKey.TryGetValue(itemKey, out existingItem))
                {
                    // a conflict was found, determine the winner.
                    var winner = ResolveConflict(existingItem, conflictItem);

                    if (winner == null)
                    {
                        // no winner, skip it.
                        // don't add to conflict list and just use the existing item for future conflicts.

                        //  Report unresolved conflict (currently just used as a test hook)
                        unresolvedConflict?.Invoke(conflictItem);

                        continue;
                    }

                    TConflictItem loser = conflictItem;
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

        readonly string SENTENCE_SPACING = "  ";

        private TConflictItem ResolveConflict(TConflictItem item1, TConflictItem item2)
        {
            string conflictMessage = string.Format(CultureInfo.CurrentCulture, Strings.EncounteredConflict,
                item1.DisplayName,
                item2.DisplayName);


            var exists1 = item1.Exists;
            var exists2 = item2.Exists;

            if (!exists1 && !exists2)
            {
                //  If neither file exists, then don't report a conflict, as both items should be resolved (or not) to the same reference assembly
                return null;
            }

            if (!exists1 || !exists2)
            {
                string fileMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.CouldNotDetermineWinner_DoesntExist,
                    !exists1 ? item1.DisplayName : item2.DisplayName);

                log.LogMessage(fileMessage);
                return null;
            }

            var assemblyVersion1 = item1.AssemblyVersion;
            var assemblyVersion2 = item2.AssemblyVersion;

            // if only one is missing version stop: something is wrong when we have a conflict between assembly and non-assembly
            if (assemblyVersion1 == null ^ assemblyVersion2 == null)
            {
                var nonAssembly = assemblyVersion1 == null ? item1.DisplayName : item2.DisplayName;
                string assemblyMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.CouldNotDetermineWinner_NotAnAssembly,
                    nonAssembly);
                
                log.LogMessage(assemblyMessage);
                return null;
            }

            // only handle cases where assembly version is different, and not null (implicit here due to xor above)
            if (assemblyVersion1 != assemblyVersion2)
            {
                string winningDisplayName;
                Version winningVersion;
                Version losingVersion;
                if (assemblyVersion1 > assemblyVersion2)
                {
                    winningDisplayName = item1.DisplayName;
                    winningVersion = assemblyVersion1;
                    losingVersion = assemblyVersion2;
                }
                else
                {
                    winningDisplayName = item2.DisplayName;
                    winningVersion = assemblyVersion2;
                    losingVersion = assemblyVersion1;
                }


                string assemblyMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingAssemblyVersion,
                    winningDisplayName,
                    winningVersion,
                    losingVersion);

                log.LogMessage(assemblyMessage);

                if (assemblyVersion1 > assemblyVersion2)
                {
                    return item1;
                }

                if (assemblyVersion2 > assemblyVersion1)
                {
                    return item2;
                }
            }

            var fileVersion1 = item1.FileVersion;
            var fileVersion2 = item2.FileVersion;

            // if only one is missing version
            if (fileVersion1 == null ^ fileVersion2 == null)
            {
                var nonVersion = fileVersion1 == null ? item1.DisplayName : item2.DisplayName;
                string fileVersionMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.CouldNotDetermineWinner_FileVersion,
                    nonVersion);
                return null;
            }

            if (fileVersion1 != fileVersion2)
            {
                string winningDisplayName;
                Version winningVersion;
                Version losingVersion;
                if (fileVersion1 > fileVersion2)
                {
                    winningDisplayName = item1.DisplayName;
                    winningVersion = fileVersion1;
                    losingVersion = fileVersion2;
                }
                else
                {
                    winningDisplayName = item2.DisplayName;
                    winningVersion = fileVersion2;
                    losingVersion = fileVersion1;
                }


                string fileVersionMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingFileVersion,
                    winningDisplayName,
                    winningVersion,
                    losingVersion);

                log.LogMessage(fileVersionMessage);

                if (fileVersion1 > fileVersion2)
                {
                    return item1;
                }

                if (fileVersion2 > fileVersion1)
                {
                    return item2;
                }
            }

            var packageRank1 = packageRank.GetPackageRank(item1.PackageId);
            var packageRank2 = packageRank.GetPackageRank(item2.PackageId);

            if (packageRank1 < packageRank2)
            {
                string packageRankMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingPreferredPackage,
                    item1.DisplayName);
                log.LogMessage(packageRankMessage);
                return item1;
            }

            if (packageRank2 < packageRank1)
            {
                string packageRankMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingPreferredPackage,
                    item2.DisplayName);
                return item2;
            }

            var isPlatform1 = item1.ItemType == ConflictItemType.Platform;
            var isPlatform2 = item2.ItemType == ConflictItemType.Platform;

            if (isPlatform1 && !isPlatform2)
            {
                string platformMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingPlatformItem,
                    item1.DisplayName);
                log.LogMessage(platformMessage);
                return item1;
            }

            if (!isPlatform1 && isPlatform2)
            {
                string platformMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingPlatformItem,
                    item2.DisplayName);
                log.LogMessage(platformMessage);
                return item2;
            }

            string message = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ConflictCouldNotDetermineWinner);

            log.LogMessage(message);
            return null;
        }
    }
}
