// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    internal delegate void ConflictCallback<T>(T winner, T loser);

    //  The conflict resolver finds conflicting items, and if there are any of them it reports the "losing" item via the foundConflict callback
    internal class ConflictResolver<TConflictItem> : IDisposable where TConflictItem : class, IConflictItem
    {
        private Dictionary<string, TConflictItem> _winningItemsByKey = new Dictionary<string, TConflictItem>();
        private Logger _log;
        private PackageRank _packageRank;
        private PackageOverrideResolver<TConflictItem> _packageOverrideResolver;
        private Dictionary<string, List<TConflictItem>> _unresolvedConflictItems = new Dictionary<string, List<TConflictItem>>(StringComparer.Ordinal);

        //  Callback for unresolved conflicts, currently just used as a test hook
        public Action<TConflictItem> UnresolvedConflictHandler { get; set; }

        public ConflictResolver(PackageRank packageRank, PackageOverrideResolver<TConflictItem> packageOverrideResolver, Logger log)
        {
            this._log = log;
            this._packageRank = packageRank;
            this._packageOverrideResolver = packageOverrideResolver;
        }

        public void ResolveConflicts(IEnumerable<TConflictItem> conflictItems, Func<TConflictItem, string> getItemKey,
            ConflictCallback<TConflictItem> foundConflict, bool commitWinner = true)
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

                if (_winningItemsByKey.TryGetValue(itemKey, out existingItem))
                {
                    // a conflict was found, determine the winner.
                    var winner = ResolveConflict(existingItem, conflictItem, logUnresolvedConflicts: false);

                    if (winner == null)
                    {
                        //  No winner.  Keep track of the conflictItem, so that if a subsequent
                        //  item wins for this key, both items (for which there was no winner when
                        //  compared to each other) can be counted as conflicts and removed from
                        //  the corresponding list.

                        List<TConflictItem> unresolvedConflictsForKey;
                        if (!_unresolvedConflictItems.TryGetValue(itemKey, out unresolvedConflictsForKey))
                        {
                            unresolvedConflictsForKey = new List<TConflictItem>();
                            _unresolvedConflictItems[itemKey] = unresolvedConflictsForKey;

                            //  This is the first time we hit an unresolved conflict for this key, so
                            //  add the existing item to the unresolved conflicts list
                            unresolvedConflictsForKey.Add(existingItem);
                        }

                        //  Add the new item to the unresolved conflicts list
                        unresolvedConflictsForKey.Add(conflictItem);

                        continue;
                    }

                    TConflictItem loser = conflictItem;
                    if (!ReferenceEquals(winner, existingItem))
                    {
                        // replace existing item
                        if (commitWinner)
                        {
                            _winningItemsByKey[itemKey] = conflictItem;
                        }
                        else
                        {
                            _winningItemsByKey.Remove(itemKey);
                        }
                        loser = existingItem;
                    }

                    foundConflict(winner, loser);

                    //  If there were any other items that tied with the loser, report them as conflicts here
                    //  if they lose against the new winner.  Otherwise, keep them in the unresolved conflict
                    //  list.
                    List<TConflictItem> previouslyUnresolvedConflicts;
                    if(_unresolvedConflictItems.TryGetValue(itemKey, out previouslyUnresolvedConflicts) &&
                        previouslyUnresolvedConflicts.Contains(loser))
                    {
                        List<TConflictItem> newUnresolvedConflicts = new List<TConflictItem>();
                        foreach (var previouslyUnresolvedItem in previouslyUnresolvedConflicts)
                        {
                            //  Don't re-report the item that just lost and was already reported
                            if (object.ReferenceEquals(previouslyUnresolvedItem, loser))
                            {
                                continue;
                            }

                            //  Call ResolveConflict with the new winner and item that previously had an unresolved
                            //  conflict, so that if the previously unresolved conflict loses, the correct message
                            //  will be logged recording that the winner won and why.  If the conflict can't be
                            //  resolved, then keep the previously unresolved conflict in the list of unresolved
                            //  conflicts.
                            var newWinner = ResolveConflict(winner, previouslyUnresolvedItem, logUnresolvedConflicts: true);
                            if (newWinner == winner)
                            {
                                foundConflict(winner, previouslyUnresolvedItem);
                            }
                            else if (newWinner == null)
                            {
                                if (newUnresolvedConflicts.Count == 0)
                                {
                                    newUnresolvedConflicts.Add(winner);
                                }
                                newUnresolvedConflicts.Add(previouslyUnresolvedItem);
                            }

                            
                        }
                        _unresolvedConflictItems.Remove(itemKey);
                        if (newUnresolvedConflicts.Count > 0)
                        {
                            _unresolvedConflictItems[itemKey] = newUnresolvedConflicts;
                        }
                    }
                }
                else if (commitWinner)
                {
                    _winningItemsByKey[itemKey] = conflictItem;
                }
            }
        }

        public void Dispose()
        {
            //  Report unresolved conflict items that didn't end up losing subsequently
            foreach (var itemKey in _unresolvedConflictItems.Keys)
            {
                //  Report the first item as an unresolved conflict
                var firstItem = _unresolvedConflictItems[itemKey][0];
                UnresolvedConflictHandler?.Invoke(firstItem);

                //  For subsequent items, report them as unresolved conflicts, and log a message
                //  that they were an unresolved conflict with the first item
                foreach (var unresolvedConflictItem in _unresolvedConflictItems[itemKey].Skip(1))
                {
                    UnresolvedConflictHandler?.Invoke(unresolvedConflictItem);

                    //  Call ResolveConflict to generate the right log message about the unresolved conflict
                    ResolveConflict(firstItem, unresolvedConflictItem, logUnresolvedConflicts: true);
                }

            }
        }

        readonly string SENTENCE_SPACING = "  ";

        private TConflictItem ResolveConflict(TConflictItem item1, TConflictItem item2, bool logUnresolvedConflicts)
        {
            var winner = _packageOverrideResolver.Resolve(item1, item2);
            if (winner != null)
            {
                return winner;
            }

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
                if (logUnresolvedConflicts)
                {
                    string fileMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.CouldNotDetermineWinner_DoesntExist,
                        !exists1 ? item1.DisplayName : item2.DisplayName);

                    _log.LogMessage(fileMessage);
                }
                return null;
            }

            var assemblyVersion1 = item1.AssemblyVersion;
            var assemblyVersion2 = item2.AssemblyVersion;

            // if only one is missing version stop: something is wrong when we have a conflict between assembly and non-assembly
            if (assemblyVersion1 == null ^ assemblyVersion2 == null)
            {
                if (logUnresolvedConflicts)
                {
                    var nonAssembly = assemblyVersion1 == null ? item1.DisplayName : item2.DisplayName;
                    string assemblyMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.CouldNotDetermineWinner_NotAnAssembly,
                        nonAssembly);

                    _log.LogMessage(assemblyMessage);
                }
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

                _log.LogMessage(assemblyMessage);

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
                if (logUnresolvedConflicts)
                {
                    var nonVersion = fileVersion1 == null ? item1.DisplayName : item2.DisplayName;
                    string fileVersionMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.CouldNotDetermineWinner_FileVersion,
                        nonVersion);
                }
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

                _log.LogMessage(fileVersionMessage);

                if (fileVersion1 > fileVersion2)
                {
                    return item1;
                }

                if (fileVersion2 > fileVersion1)
                {
                    return item2;
                }
            }

            var packageRank1 = _packageRank.GetPackageRank(item1.PackageId);
            var packageRank2 = _packageRank.GetPackageRank(item2.PackageId);

            if (packageRank1 < packageRank2)
            {
                string packageRankMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingPreferredPackage,
                    item1.DisplayName);
                _log.LogMessage(packageRankMessage);
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
                _log.LogMessage(platformMessage);
                return item1;
            }

            if (!isPlatform1 && isPlatform2)
            {
                string platformMessage = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ChoosingPlatformItem,
                    item2.DisplayName);
                _log.LogMessage(platformMessage);
                return item2;
            }

            if (logUnresolvedConflicts)
            {
                string message = conflictMessage + SENTENCE_SPACING + string.Format(CultureInfo.CurrentCulture, Strings.ConflictCouldNotDetermineWinner);

                _log.LogMessage(message);
            }
            return null;
        }
    }
}
