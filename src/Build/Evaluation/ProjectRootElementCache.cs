// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;
using OutOfProcNode = Microsoft.Build.Execution.OutOfProcNode;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Maintains a cache of all loaded ProjectRootElement's for design time purposes.
    /// Weak references are held to add added ProjectRootElement's.
    /// Strong references are held to a limited number of added ProjectRootElement's.
    ///
    /// 1. Loads of a ProjectRootElement will share any existing loaded ProjectRootElement, rather
    /// than loading and parsing a new one. This is the case whether the ProjectRootElement
    /// is loaded directly or imported.
    ///
    /// 2. For design time, only a weak reference needs to be held, because all users have a strong reference.
    ///
    /// 3. Because all loads of a ProjectRootElement consult this cache, they can be assured that any
    /// entries in this cache are up to date. For example, if a ProjectRootElement is modified and saved,
    /// the cached ProjectRootElement will be the loaded one that was saved, so it will be up to date.
    ///
    /// 4. If, after a project has been loaded, an external app changes the project file content on disk, it is
    /// important that a subsequent load of that project does not return stale ProjectRootElement. To avoid this, the
    /// timestamp of the file on disk is compared to the timestamp of the file at the time that the ProjectRootElement loaded it.
    ///
    /// 5. For build time, some strong references need to be held, as otherwise the ProjectRootElement's for reuseable
    /// imports will be collected, and time will be wasted reparsing them. However we do not want to hold strong references
    /// to all ProjectRootElement's, consuming memory without end. So a simple priority queue is used. All Adds and Gets boost their
    /// entry to the top. As the queue gets too big, low priority entries are dropped.
    ///
    /// No guesses are made at which files are more interesting to cache, beyond the most-recently-used list. For example, ".targets" files
    /// or imported files are not treated specially, as this is a potentially unreliable heuristic. Besides, caching a project file itself could
    /// be useful, if for example you want to build it twice with different sets of properties.
    ///
    /// Because of the strongly typed list, some ProjectRootElement's will be held onto indefinitely. This is an acceptable price to pay for
    /// being able to provide a commonly used ProjectRootElement immediately it's needed. It is mitigated by the list being finite and small, and
    /// because we allow ProjectCollection.UnloadAllProjects to hint to us to clear the list.
    ///
    /// Implicit references are those which were loaded as a result of a build, and not explicitly loaded through, for instance, the project
    /// collection.
    ///
    /// </summary>
    internal class ProjectRootElementCache : ProjectRootElementCacheBase
    {
        /// <summary>
        /// The maximum number of entries to keep strong references to.
        /// This has to be strong enough to make sure that key .targets files aren't pushed
        /// off by transient loads of non-reusable files like .user files.
        /// </summary>
        /// <remarks>
        /// Made this as large as 200 because ASP.NET Core (6.0) projects have
        /// something like 80-90 imports. This was observed to give a noticeable
        /// performance improvement compared to a mid-17.0 MSBuild with the old
        /// value of 50.
        ///
        /// If this number is increased much higher, the datastructure may
        /// need to be changed from a linked list, since it's currently O(n).
        /// </remarks>
        private static readonly int s_maximumStrongCacheSize =
            int.TryParse(Environment.GetEnvironmentVariable("MSBUILDPROJECTROOTELEMENTCACHESIZE"), out int cacheSize) ? cacheSize : 200;

        /// <summary>
        /// Whether the cache should log activity to the Debug.Out stream
        /// </summary>
        private static readonly bool s_debugLogCacheActivity = Environment.GetEnvironmentVariable("MSBUILDDEBUGXMLCACHE") == "1";

        /// <summary>
        /// Whether the cache should check file content for cache entry invalidation.
        /// </summary>
        /// <remarks>
        /// Value shall be true only in case of testing. Outside QA tests it shall be false.
        /// </remarks>
        private static readonly bool s_сheckFileContent = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDCACHECHECKFILECONTENT"));

#if DEBUG
        /// <summary>
        /// A simple IDisposable struct implementing the holder/guard pattern over the Get reentrancy counter.
        /// </summary>
        private struct ReentrancyGuard : IDisposable
        {
            /// <summary>
            /// Number of entries into Get function of the ProjectRootElementCache.
            /// Shall be always 0 or 1. Reentrance to the Get function (value > 1) could lead to race condition.
            /// </summary>
            [ThreadStatic]
            private static int s_getEntriesNumber;

            public ReentrancyGuard()
            {
                s_getEntriesNumber++;
                ErrorUtilities.VerifyThrow(
                    s_getEntriesNumber == 1,
                    "Reentrance to the ProjectRootElementCache.Get function detected.");
            }

            public void Dispose()
            {
                s_getEntriesNumber--;
            }
        }
#endif

        /// <summary>
        /// The map of weakly-held ProjectRootElement's keyed by full path.
        /// </summary>
        private readonly ConcurrentWeakValueDictionary<string, ProjectRootElement> _weakCache;

        /// <summary>
        /// Lock objects keyed by project file path. Used to serialize concurrent loads
        /// (and the rare <see cref="ProjectRootElement.Reload"/> on preserveFormatting mismatch)
        /// for the same file.
        /// </summary>
        private readonly ConcurrentDictionary<string, LockType> _fileLoadLocks;

        /// <summary>
        /// The LRU list of strongly-held ProjectRootElement's, preventing GC of commonly used entries.
        /// </summary>
        /// <remarks>
        /// Mutated only while holding <see cref="_strongCacheLock"/>.
        /// </remarks>
        private readonly LinkedList<ProjectRootElement> _strongCacheList;

        /// <summary>
        /// Index from <see cref="ProjectRootElement"/> to its node in <see cref="_strongCacheList"/>
        /// so that <see cref="BoostEntryInStrongCache"/> is O(1) instead of O(n). Uses reference identity.
        /// </summary>
        /// <remarks>
        /// Mutated only while holding <see cref="_strongCacheLock"/>.
        /// </remarks>
        private readonly Dictionary<ProjectRootElement, LinkedListNode<ProjectRootElement>> _strongCacheIndex;

        /// <summary>
        /// Whether the cache should check the timestamp of the file on disk
        /// whenever it is requested, and update with the latest content of that
        /// file if it has changed.
        /// </summary>
        private readonly bool _autoReloadFromDisk;

        /// <summary>
        /// Lock protecting the strong cache (<see cref="_strongCacheList"/>, <see cref="_strongCacheIndex"/>)
        /// and serializing any multi-step write that must remain atomic across the strong cache and
        /// <see cref="_weakCache"/> together (e.g., <see cref="RenameEntryInternal"/>).
        ///
        /// This lock serializes every writer to <see cref="_weakCache"/> that
        /// inserts or replaces a value. The only writers that bypass it — <see cref="DiscardAnyWeakReference"/>
        /// and the dictionary's internal weak-reference sweep — can only remove an entry; they never replace it with a different one. 
        /// So while inside this lock, the value under any given key either stays the same or disappears entirely.
        /// It is never silently swapped for a different instance.
        ///
        /// Hot-path reads of <see cref="_weakCache"/> do NOT take this lock.
        ///
        /// Lock ordering: a per-file lock from <see cref="_fileLoadLocks"/> may be held while acquiring
        /// this lock, but never the reverse.
        /// </summary>
        private readonly LockType _strongCacheLock = new LockType();

        /// <summary>
        /// Creates an empty cache.
        /// </summary>
        internal ProjectRootElementCache(bool autoReloadFromDisk, bool loadProjectsReadOnly = false)
        {
            DebugTraceCache("Constructing with autoreload from disk: ", autoReloadFromDisk);

            _weakCache = new ConcurrentWeakValueDictionary<string, ProjectRootElement>(StringComparer.OrdinalIgnoreCase);
            _strongCacheList = new LinkedList<ProjectRootElement>();
            _strongCacheIndex = new Dictionary<ProjectRootElement, LinkedListNode<ProjectRootElement>>(ReferenceComparer.Instance);
            _fileLoadLocks = new ConcurrentDictionary<string, LockType>(StringComparer.OrdinalIgnoreCase);
            _autoReloadFromDisk = autoReloadFromDisk;
            LoadProjectsReadOnly = loadProjectsReadOnly;
        }


        /// <summary>
        /// Returns true if given cache entry exists and is outdated.
        /// </summary>
        private bool IsInvalidEntry(string projectFile, ProjectRootElement projectRootElement)
        {
            // When we do not _autoReloadFromDisk we expect that cached value is always valid.
            // Usually lifespan of cache is expected to be build duration (process will terminate after build).
            if (projectRootElement == null || !_autoReloadFromDisk)
            {
                return false;
            }

            // If the project file is non modifiable, assume it is up to date and consider the cached value valid.
            if (!Traits.Instance.EscapeHatches.AlwaysDoImmutableFilesUpToDateCheck && FileClassifier.Shared.IsNonModifiable(projectFile))
            {
                return false;
            }

            FileInfo fileInfo = FileUtilities.GetFileInfoNoThrow(projectFile);

            // If the file doesn't exist on disk, go ahead and use the cached version.
            // It's an in-memory project that hasn't been saved yet.
            if (fileInfo == null)
            {
                return false;
            }

            if (fileInfo.LastWriteTime != projectRootElement.LastWriteTimeWhenRead)
            {
                // File was changed on disk by external means. Cached version is no longer valid.
                // We could throw here or ignore the problem, but it is a common and reasonable pattern to change a file
                // externally and load a new project over it to see the new content. So we dump it from the cache
                // to force a load from disk. There might then exist more than one ProjectRootElement with the same path,
                // but clients ought not get themselves into such a state - and unless they save them to disk,
                // it may not be a problem.
                return true;
            }
            else if (s_сheckFileContent)
            {
                // QA tests run too fast for the timestamp check to work. This environment variable is for their
                // use: it checks the file content as well as the timestamp. That's better than completely disabling
                // the cache as we get test coverage of the rest of the cache code.
                XmlDocument document = new XmlDocument();
                document.PreserveWhitespace = projectRootElement.XmlDocument.PreserveWhitespace;

                using (var xtr = XmlReaderExtension.Create(projectRootElement.FullPath, projectRootElement.ProjectRootElementCache.LoadProjectsReadOnly))
                {
                    document.Load(xtr.Reader);
                }

                string diskContent = document.OuterXml;
                string cacheContent = projectRootElement.XmlDocument.OuterXml;

                if (diskContent != cacheContent)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns an existing ProjectRootElement for the specified file path, if any.
        /// If none exists, calls the provided delegate to load one, and adds that to the cache.
        /// The reason that it calls back to do this is so that the cache is locked between determining
        /// that the entry does not exist and adding the entry.
        ///
        /// If <see cref="_autoReloadFromDisk"/> was set to true, and the file on disk has changed since it was cached,
        /// it will be reloaded before being returned.
        ///
        /// Thread safe.
        /// </summary>
        /// <remarks>
        /// Never needs to consult the strong cache as well, since if the item is in there, it will
        /// not have left the weak cache.
        /// If item is found, boosts it to the top of the strong cache.
        /// </remarks>
        /// <param name="projectFile">The project file which contains the ProjectRootElement.  Must be a full path.</param>
        /// <param name="loadProjectRootElement">The delegate to use to load if necessary. May be null. Must not update the cache.</param>
        /// <param name="isExplicitlyLoaded"><code>true</code> if the project is explicitly loaded, otherwise <code>false</code>.</param>
        /// <param name="preserveFormatting"><code>true</code> to the project was loaded with the formated preserved, otherwise <code>false</code>.</param>
        /// <returns>The ProjectRootElement instance if one exists.  Null otherwise.</returns>
        internal override ProjectRootElement Get(string projectFile, OpenProjectRootElement loadProjectRootElement, bool isExplicitlyLoaded,
            bool? preserveFormatting)
        {
#if DEBUG
            // Verify that loadProjectRootElement delegate does not call ProjectRootElementCache.Get().
            using var reentrancyGuard = new ReentrancyGuard();

            // Verify that we never call this with _strongCacheLock held, as that would create a lock ordering inversion with the per-file lock.
            ErrorUtilities.VerifyThrow(
                !IsLockHeld(_strongCacheLock),
                "Detected lock ordering inversion in ProjectRootElementCache.");
#endif
            // Should already have been canonicalized
            ErrorUtilities.VerifyThrowInternalRooted(projectFile);

            // First try getting the ProjectRootElement from the cache.
            ProjectRootElement projectRootElement = GetOrLoad(projectFile, loadProjectRootElement: null, isExplicitlyLoaded, preserveFormatting);

            if (projectRootElement != null || loadProjectRootElement == null)
            {
                // If we found it or no load callback was specified, we are done.
                return projectRootElement;
            }

            try
            {
                // We are about to load. Take a per-file lock to prevent multiple threads from duplicating the work multiple times.
                LockType perFileLock = _fileLoadLocks.GetOrAdd(projectFile, static _ => new LockType());
                lock (perFileLock)
                {
                    // Call GetOrLoad again, this time with the OpenProjectRootElement callback.
                    return GetOrLoad(projectFile, loadProjectRootElement, isExplicitlyLoaded, preserveFormatting);
                }
            }
            finally
            {
                // Remove the lock object as we have otherwise no good way of preventing _fileLoadLocks from growing unboundedly.
                // If another thread is inside the lock, we effectively create a race condition where someone else may enter
                // GetOrLoad. This is OK because this fine-grained locking is just a perf optimization, and we have either loaded
                // the ProjectRootElement by now, or it is an error condition where perf is not critical.
                _fileLoadLocks.TryRemove(projectFile, out _);
            }
        }

        /// <summary>
        /// A helper used by <see cref="Get"/>.
        /// </summary>
        private ProjectRootElement GetOrLoad(string projectFile, OpenProjectRootElement loadProjectRootElement, bool isExplicitlyLoaded,
            bool? preserveFormatting)
        {
            ProjectRootElement projectRootElement = null;

            // Hot path: lock-free lookup in the concurrent weak-value dictionary.
            if (_weakCache.TryGetValue(projectFile, out projectRootElement))
            {
                // Boost in the strong-cache LRU. This is now an O(1) operation guarded by a
                // narrow lock, so we always take it (skipping under contention would corrupt
                // LRU eviction order under exactly the workload we are trying to fix).
                lock (_strongCacheLock)
                {
                    BoostEntryInStrongCache(projectRootElement);
                }

                // An implicit load will never reset the explicit flag.
                // Setting this flag is safe outside the lock because IsExplicitlyLoaded
                // is backed by a volatile field and is monotonic (false -> true only).
                if (isExplicitlyLoaded)
                {
                    projectRootElement.MarkAsExplicitlyLoaded();
                }

                if (preserveFormatting != null && projectRootElement.XmlDocument.PreserveWhitespace != preserveFormatting)
                {
                    // Serialize concurrent Reloads of the same file so two writers cannot interleave
                    // a tear-down/reparse of the same XmlDocument.

                    // Note: this lock only protects Reload-vs-Reload, not Reload-vs-read. Callers that
                    // already hold a reference to this PRE may observe a half-reloaded XmlDocument.
                    LockType perFileLock = _fileLoadLocks.GetOrAdd(projectFile, static _ => new LockType());
                    lock (perFileLock)
                    {
                        if (projectRootElement.XmlDocument.PreserveWhitespace != preserveFormatting)
                        {
                            projectRootElement.Reload(true, preserveFormatting);
                        }
                    }
                }
            }
            else
            {
                DebugTraceCache("Not found in cache: ", projectFile);
            }

            bool projectRootElementIsInvalid = IsInvalidEntry(projectFile, projectRootElement);
            if (projectRootElementIsInvalid)
            {
                DebugTraceCache("Not satisfied from cache: ", projectFile);
                ForgetEntryIfExists(projectRootElement);
            }

            if (loadProjectRootElement == null)
            {
                if (projectRootElement == null || projectRootElementIsInvalid)
                {
                    return null;
                }
                else
                {
                    DebugTraceCache("Satisfied from XML cache: ", projectFile);
                    return projectRootElement;
                }
            }

            // Use openProjectRootElement to reload the element if the cache element does not exist or need to be reloaded.
            if (projectRootElement == null || projectRootElementIsInvalid)
            {
                projectRootElement = loadProjectRootElement(projectFile, this);
                ErrorUtilities.VerifyThrowInternalNull(projectRootElement, "projectRootElement");
                ErrorUtilities.VerifyThrow(
                    projectRootElement.FullPath.Equals(projectFile, StringComparison.OrdinalIgnoreCase),
                    $"Got project back with incorrect path. Expected path: {projectFile}, received path: {projectRootElement.FullPath}.");

                // An implicit load will never reset the explicit flag.
                if (isExplicitlyLoaded)
                {
                    projectRootElement.MarkAsExplicitlyLoaded();
                }

                // Update cache element.
                // It is unlikely, but it might be that while without the lock, the projectRootElement in cache was updated by another thread.
                // And here its entry will be replaced with the loaded projectRootElement. This is fine:
                // if loaded projectRootElement is out of date (so, it changed since the time we loaded it), it will be updated the next time some thread calls Get function.
                AddEntry(projectRootElement);
            }
            else
            {
                DebugTraceCache("Satisfied from XML cache: ", projectFile);
            }

            return projectRootElement;
        }

        /// <summary>
        /// Add an entry to the cache.
        /// </summary>
        /// <remarks>
        /// The <see cref="ProjectRootElementCacheBase.ProjectRootElementAddedHandler"/> event is
        /// raised OUTSIDE <see cref="_strongCacheLock"/>. This has two observable consequences:
        ///
        /// 1. Re-entrancy model: subscribers may freely call back into any cache API
        ///    (e.g., <see cref="Get"/>, <see cref="TryGet(string)"/>) from their handler — the
        ///    cache is fully unlocked when the event fires. However, the cache no longer supports
        ///    same-thread recursive acquisition of <see cref="_strongCacheLock"/>: any code
        ///    path that ends up re-entering a lock-taking cache method while already holding
        ///    <see cref="_strongCacheLock"/> on the same thread will throw
        ///    <see cref="System.Threading.LockRecursionException"/> (since <see cref="LockType"/>
        ///    is non-recursive on net9+). All internal helpers that require the lock to be held
        ///    are private and asserted via <c>IsLockHeld</c>; public entry points always start
        ///    from an unlocked state.
        ///
        /// 2. Event ordering: under concurrent <see cref="AddEntry"/> calls, the order in which
        ///    the cache-added event fires is NOT guaranteed to match the order in which entries
        ///    were published into the cache. A thread may release the lock and then be preempted
        ///    before raising its event, while another thread completes its own add-and-raise in
        ///    between. Subscribers must not rely on the event order reflecting the addition
        ///    order; the event is a per-entry notification, not an ordered log of cache mutations.
        /// </remarks>
        internal override void AddEntry(ProjectRootElement projectRootElement)
        {
            lock (_strongCacheLock)
            {
                RenameEntryInternal(null, projectRootElement);
            }

            // Fire the event outside the LRU lock: prevents subscriber work from stalling other
            // cache operations, and avoids LockRecursionException if a subscriber re-enters the
            // cache on the same thread (LockType is non-recursive on net9+). See remarks above
            // for the two limitations this introduces.
            RaiseProjectRootElementAddedToCacheEvent(projectRootElement);
        }

        /// <summary>
        /// Rename an entry in the cache.
        /// Entry must already be in the cache.
        /// </summary>
        internal override void RenameEntry(string oldFullPath, ProjectRootElement projectRootElement)
        {
            lock (_strongCacheLock)
            {
                ErrorUtilities.VerifyThrowArgumentLength(oldFullPath);
                RenameEntryInternal(oldFullPath, projectRootElement);
            }
        }

        /// <summary>
        /// Returns any a ProjectRootElement in the cache with the provided full path,
        /// otherwise null.
        /// </summary>
        internal override ProjectRootElement TryGet(string projectFile)
        {
            return TryGet(projectFile, preserveFormatting: null);
        }

        /// <summary>
        /// Returns any a ProjectRootElement in the cache with the provided full path,
        /// otherwise null.
        /// </summary>
        internal override ProjectRootElement TryGet(string projectFile, bool? preserveFormatting)
        {
            ProjectRootElement result = Get(
                projectFile,
                loadProjectRootElement: null, // no delegate to load it
                isExplicitlyLoaded: false, // Since we are not creating a PRE this can be true or false
                preserveFormatting: preserveFormatting);

            return result;
        }

        /// <summary>
        /// Discards strong references held by the cache.
        /// </summary>
        /// <remarks>
        /// The weak cache is never cleared, as we need it to guarantee that the appdomain never
        /// has two ProjectRootElement's for a particular file. Attempts to clear out the weak cache
        /// resulted in this guarantee being broken and subtle bugs popping up everywhere.
        /// </remarks>
        internal override void DiscardStrongReferences()
        {
            lock (_strongCacheLock)
            {
                DebugTraceCache("Clearing strong refs: ", _strongCacheList.Count);

                _strongCacheList.Clear();
                _strongCacheIndex.Clear();

                // A scavenge of the weak cache is probably not worth it as
                // the GC would have had to run immediately after the line above.
            }
        }

        /// <summary>
        /// Clears out the cache.
        /// Called when all projects are unloaded and possibly when a build is done.
        /// </summary>
        internal override void Clear()
        {
            lock (_strongCacheLock)
            {
                _weakCache.Clear();
                _strongCacheList.Clear();
                _strongCacheIndex.Clear();
            }
        }

        /// <summary>
        /// Discard any entries (weak and strong) which do not have the explicitlyLoaded flag set.
        /// </summary>
        internal override void DiscardImplicitReferences()
        {
            if (_autoReloadFromDisk)
            {
                // no need to clear it, as auto reload properly invalidates caches if changed.
                return;
            }

            lock (_strongCacheLock)
            {
                // Drop everything from the strong cache that is not explicitly loaded.
                LinkedListNode<ProjectRootElement> node = _strongCacheList.First;
                while (node != null)
                {
                    LinkedListNode<ProjectRootElement> next = node.Next;
                    if (!node.Value.IsExplicitlyLoaded)
                    {
                        _strongCacheIndex.Remove(node.Value);
                        _strongCacheList.Remove(node);
                    }
                    node = next;
                }

                // Drop weak-cache entries that are either dead or not explicitly loaded.
                // Enumeration is snapshot-ish; entries added during enumeration will simply remain
                // (which is fine: they were added after the caller decided to discard implicit refs).
                foreach (KeyValuePair<string, ProjectRootElement> kvp in _weakCache)
                {
                    if (!kvp.Value.IsExplicitlyLoaded)
                    {
                        // Only delete if the slot still holds this exact PRE, so a fresh entry
                        // published for the same path by another writer is preserved.
                        _weakCache.TryRemove(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Forces a removal of a project root element from the weak cache if it is present.
        /// </summary>
        /// <param name="projectRootElement">The project root element to remove.</param>
        /// <remarks>
        /// No exception is thrown if this project root element is in use by currently loaded projects
        /// by this method.  The calling method must know that this is a safe operation.
        /// There may of course be strong references to the project root element from customer code.
        /// The assumption is that when they instruct the project collection to unload it, which
        /// leads to this being called, they are releasing their strong references too (or it doesn't matter)
        /// </remarks>
        internal override void DiscardAnyWeakReference(ProjectRootElement projectRootElement)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectRootElement);

            // A PRE may be unnamed if it was only used in memory.
            if (projectRootElement.FullPath != null)
            {
                // Only delete if the slot still maps to this PRE — otherwise we'd clobber
                // a fresh entry that another thread just inserted for the same path.
                _weakCache.TryRemove(projectRootElement.FullPath, projectRootElement);
            }
        }

        /// <summary>
        /// Add or rename an entry in the cache.
        /// Old full path may be null iff it was not already in the cache.
        /// </summary>
        /// <remarks>
        /// Must be called within <see cref="_strongCacheLock"/>.
        /// </remarks>
        private void RenameEntryInternal(string oldFullPathIfAny, ProjectRootElement projectRootElement)
        {
#if DEBUG
            Debug.Assert(IsLockHeld(_strongCacheLock), "RenameEntryInternal must be called under _strongCacheLock.");
#endif
            ErrorUtilities.VerifyThrowInternalNull(projectRootElement.FullPath, "FullPath");

            if (oldFullPathIfAny != null)
            {
                ErrorUtilities.VerifyThrowInternalRooted(oldFullPathIfAny);
                ErrorUtilities.VerifyThrow(
                    _weakCache.TryGetValue(oldFullPathIfAny, out ProjectRootElement oldPre) &&
                    ReferenceEquals(oldPre, projectRootElement),
                    "Should already be present");
                _weakCache.TryRemove(oldFullPathIfAny);
            }

            // There may already be a ProjectRootElement in the cache with the new name. In this case we cannot throw an exception;
            // we must merely replace it. This is because it may be an unrooted entry
            // (and thus gone from the client's point of view) that merely remains
            // in the cache because we still have a reference to it from our strong cache.
            // Another possibility is that there are two, unrelated, un-saved, in-memory projects that were given the same path.
            // Replacing the cache entry does not in itself cause a problem -- if there are any actual users of the old
            // entry they will not be affected. There would then exist more than one ProjectRootElement with the same path,
            // but clients ought not get themselves into such a state - and unless they save them to disk,
            // it may not be a problem. Replacing also doesn't cause a problem for the strong cache,
            // as it is never consulted by us, but it is reasonable for us to remove the old entry in that case.
            if (_weakCache.TryGetValue(projectRootElement.FullPath, out ProjectRootElement existingWeakEntry) &&
                !ReferenceEquals(existingWeakEntry, projectRootElement))
            {
                RemoveFromStrongCache(existingWeakEntry);
            }

            DebugTraceCache("Adding: ", projectRootElement.FullPath);
            _weakCache[projectRootElement.FullPath] = projectRootElement;

            BoostEntryInStrongCache(projectRootElement);
        }

        /// <summary>
        /// Update the strong cache.
        /// If the item is already a member of the list, move it to the top.
        /// Otherwise, just add it to the top.
        /// If the list is too large, remove an entry from the bottom.
        /// </summary>
        /// <remarks>
        /// Must be called within <see cref="_strongCacheLock"/>. O(1) via <see cref="_strongCacheIndex"/>.
        /// </remarks>
        private void BoostEntryInStrongCache(ProjectRootElement projectRootElement)
        {
#if DEBUG
            Debug.Assert(IsLockHeld(_strongCacheLock), "BoostEntryInStrongCache must be called under _strongCacheLock.");
#endif
            if (_strongCacheIndex.TryGetValue(projectRootElement, out LinkedListNode<ProjectRootElement> node))
            {
                // Already in strong cache — move to front.
                if (!ReferenceEquals(node, _strongCacheList.First))
                {
                    _strongCacheList.Remove(node);
                    _strongCacheList.AddFirst(node);
                }
            }
            else
            {
                // New entry — add to front.
                LinkedListNode<ProjectRootElement> newNode = _strongCacheList.AddFirst(projectRootElement);
                _strongCacheIndex[projectRootElement] = newNode;

                if (_strongCacheList.Count > s_maximumStrongCacheSize)
                {
                    LinkedListNode<ProjectRootElement> last = _strongCacheList.Last;
                    DebugTraceCache("Shedding: ", last.Value.FullPath);
                    _strongCacheList.RemoveLast();
                    _strongCacheIndex.Remove(last.Value);
                }
            }
        }

        /// <summary>
        /// Remove an entry from the strong cache (both list and index).
        /// </summary>
        /// <remarks>
        /// Must be called within <see cref="_strongCacheLock"/>.
        /// </remarks>
        private void RemoveFromStrongCache(ProjectRootElement projectRootElement)
        {
#if DEBUG
            Debug.Assert(IsLockHeld(_strongCacheLock), "RemoveFromStrongCache must be called under _strongCacheLock.");
#endif
            if (_strongCacheIndex.TryGetValue(projectRootElement, out LinkedListNode<ProjectRootElement> node))
            {
                _strongCacheList.Remove(node);
                _strongCacheIndex.Remove(projectRootElement);
            }
        }

        /// <summary>
        /// Completely remove an entry from this cache.
        /// </summary>
        /// <remarks>
        /// Must be called within <see cref="_strongCacheLock"/>.
        /// </remarks>
        private void ForgetEntry(ProjectRootElement projectRootElement)
        {
#if DEBUG
            Debug.Assert(IsLockHeld(_strongCacheLock), "ForgetEntry must be called under _strongCacheLock.");
#endif
            DebugTraceCache("Forgetting: ", projectRootElement.FullPath);

            _weakCache.TryRemove(projectRootElement.FullPath);
            RemoveFromStrongCache(projectRootElement);

            DebugTraceCache("Out of date dropped from XML cache: ", projectRootElement.FullPath);
        }

        /// <summary>
        /// Completely remove an entry from this cache if it exists and still maps to the
        /// expected <see cref="ProjectRootElement"/> instance.
        /// </summary>
        private void ForgetEntryIfExists(ProjectRootElement projectRootElement)
        {
            lock (_strongCacheLock)
            {
                // The caller's reference came from an earlier lock-free read, so the
                // slot may now hold a fresh entry for the same path. Per the invariant on
                // _strongCacheLock, no swap can occur while inside the lock — so a matching reference
                // means it is safe to evict; a mismatch means a fresh entry was published and we leave it alone.
                if (_weakCache.TryGetValue(projectRootElement.FullPath, out ProjectRootElement cached) &&
                    ReferenceEquals(cached, projectRootElement))
                {
                    ForgetEntry(projectRootElement);
                }
            }
        }

#if DEBUG
        /// <summary>
        /// Returns true if the given lock is held by the current thread.
        /// Encapsulates the difference between the lock type used on net9+ and on net472.
        /// </summary>
        private static bool IsLockHeld(LockType lockObj) =>
#if NET
            lockObj.IsHeldByCurrentThread;
#else
            System.Threading.Monitor.IsEntered(lockObj);
#endif
#endif

        /// <summary>
        /// Equality comparer using reference identity for <see cref="ProjectRootElement"/>.
        /// Used by <see cref="_strongCacheIndex"/> to match entries by object identity, not value equality.
        /// </summary>
        private sealed class ReferenceComparer : IEqualityComparer<ProjectRootElement>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(ProjectRootElement x, ProjectRootElement y) => ReferenceEquals(x, y);

            public int GetHashCode(ProjectRootElement obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Write debugging messages to the Debug.Out stream.
        /// </summary>
        private void DebugTraceCache(string message, bool param1)
        {
            if (s_debugLogCacheActivity)
            {
                DebugTraceCache(message, Convert.ToString(param1, CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Write debugging messages to the Debug.Out stream.
        /// </summary>
        private void DebugTraceCache(string message, int param1)
        {
            if (s_debugLogCacheActivity)
            {
                DebugTraceCache(message, Convert.ToString(param1, CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Write debugging messages to the Debug.Out stream.
        /// </summary>
        private void DebugTraceCache(string message, string param1)
        {
            if (s_debugLogCacheActivity)
            {
                string prefix = OutOfProcNode.IsOutOfProcNode ? "C" : "P";
                Trace.WriteLine($"{prefix} {Process.GetCurrentProcess().Id} | {message}{param1}");
            }
        }
    }
}
