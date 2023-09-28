// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    internal delegate void ConflictCallback<T>(T winner, T loser);

    //  The conflict resolver finds conflicting items, and if there are any of them it reports the "losing" item via the foundConflict callback
    internal class ConflictResolver<TConflictItem> : IDisposable where TConflictItem : class, IConflictItem
    {
        private Dictionary<string, TConflictItem> _winningItemsByKey = new();
        private Logger _log;
        private PackageRank _packageRank;
        private PackageOverrideResolver<TConflictItem> _packageOverrideResolver;
        private Dictionary<string, List<TConflictItem>> _unresolvedConflictItems = new(StringComparer.Ordinal);

        //  Callback for unresolved conflicts, currently just used as a test hook
        public Action<TConflictItem> UnresolvedConflictHandler { get; set; }

        public ConflictResolver(PackageRank packageRank, PackageOverrideResolver<TConflictItem> packageOverrideResolver, Logger log)
        {
            _log = log;
            _packageRank = packageRank;
            _packageOverrideResolver = packageOverrideResolver;
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

                if (string.IsNullOrEmpty(itemKey))
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
                    if (_unresolvedConflictItems.TryGetValue(itemKey, out previouslyUnresolvedConflicts) &&
                        previouslyUnresolvedConflicts.Contains(loser))
                    {
                        List<TConflictItem> newUnresolvedConflicts = new();
                        foreach (var previouslyUnresolvedItem in previouslyUnresolvedConflicts)
                        {
                            //  Don't re-report the item that just lost and was already reported
                            if (ReferenceEquals(previouslyUnresolvedItem, loser))
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

        private TConflictItem ResolveConflict(TConflictItem item1, TConflictItem item2, bool logUnresolvedConflicts)
        {
            var winner = _packageOverrideResolver.Resolve(item1, item2);
            if (winner != null)
            {
                return winner;
            }

            string conflictMessage = string.Format(CultureInfo.CurrentCulture, Strings.EncounteredConflict_Info,
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
                    LogMessage(conflictMessage, Strings.CouldNotDetermineWinner_DoesNotExist_Info,
                        !exists1 ? item1.DisplayName : item2.DisplayName);
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
                    LogMessage(conflictMessage, Strings.CouldNotDetermineWinner_NotAnAssembly_Info,
                        nonAssembly);
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


                LogMessage(conflictMessage, Strings.ChoosingAssemblyVersion_Info,
                    winningDisplayName,
                    winningVersion,
                    losingVersion);

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
                    LogMessage(conflictMessage, Strings.CouldNotDetermineWinner_NoFileVersion_Info, nonVersion);
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

                LogMessage(conflictMessage, Strings.ChoosingFileVersion_Info,
                    winningDisplayName,
                    winningVersion,
                    losingVersion);

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
                LogMessage(conflictMessage, Strings.ChoosingPreferredPackage_Info, item1.DisplayName);

                return item1;
            }

            if (packageRank2 < packageRank1)
            {
                LogMessage(conflictMessage, Strings.ChoosingPreferredPackage_Info, item2.DisplayName);
                return item2;
            }

            var isPlatform1 = item1.ItemType == ConflictItemType.Platform;
            var isPlatform2 = item2.ItemType == ConflictItemType.Platform;

            if (isPlatform1 && !isPlatform2)
            {
                LogMessage(conflictMessage, Strings.ChoosingPlatformItem_Info, item1.DisplayName);
                return item1;
            }

            if (!isPlatform1 && isPlatform2)
            {
                LogMessage(conflictMessage, Strings.ChoosingPlatformItem_Info, item2.DisplayName);
                return item2;
            }

            if (item1.ItemType == ConflictItemType.CopyLocal && item2.ItemType == ConflictItemType.CopyLocal)
            {
                // If two items are copy local, we must pick one even if versions are identical, as only 
                // one of them can be copied locally. The policy here must be deterministic, but it can
                // be chosen arbitrarily. The assumption is that the assemblies are fully semantically 
                // equivalent.
                //
                // We choose ordinal string comparison of package id as a final tie-breaker for this case. 
                // We will get here in the real case of frameworks with overlapping assemblies (including 
                // version) and self-contained apps. The assembly we choose here is not guaranteed to match
                // the assembly that would be chosen by the host for a framework-dependent app. The host
                // is free to make its own deterministic but arbitrary choice.
                int cmp = string.CompareOrdinal(item1.PackageId, item2.PackageId);
                if (cmp != 0)
                {
                    var arbitraryWinner = cmp < 0 ? item1 : item2;
                    LogMessage(conflictMessage, Strings.ChoosingCopyLocalArbitrarily_Info, arbitraryWinner.DisplayName);
                    return arbitraryWinner;
                }
            }

            if (logUnresolvedConflicts)
            {
                LogMessage(conflictMessage, Strings.CouldNotDetermineWinner_EqualVersions_Info);
            }
            return null;
        }

        private void LogMessage(string conflictMessage, string format, params object[] args)
        {
            _log.LogMessage(
                MessageImportance.Low,
                conflictMessage + " " + string.Format(format, args));
        }
    }
}
