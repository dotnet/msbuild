// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Globalization;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// The purpose of this class is to contain a set of cache entries which belong to
    /// a particular scope. The scope is defined as string (project file name) X set of
    /// string ( global properties ). This class is thread safe and can be used from multiple
    /// threads. It is also lock free for multiple readers (via use of Hashtable as
    /// the backing store).
    /// We use a ReaderWriterLock in here so that when a request comes in for multiple
    /// cache entries we guarantee that all the entries come from a consistent view of 
    /// the cache at some point in time. Without a lock we might get a write in between
    /// the reads and half of the entries would represent the values before the write and 
    /// the rest after. This is unacceptable as at no point in time the cache actually contained
    /// the entries that would be returned without the lock. This is required by the caching APIs
    /// provided to the tasks which support retrieving multiple entries at once.
    /// </summary>
    internal class CacheScope
    {
        #region Constructors

        /// <summary>
        /// This constructor creates a scope for a particular name and set of properties
        /// </summary>
        internal CacheScope(string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion)
        {
            // Make certain we don't cache a reference to a Project object, which would defeat 
            // the purpose of this cache
            scopeProperties.ClearParentProject(); 
            
            this.scopeName = scopeName;
            this.scopeToolsVersion = scopeToolsVersion;
            this.scopeProperties = scopeProperties;
            this.cacheContents = new Hashtable(StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Set of cached project file properties
        /// </summary>
        internal BuildPropertyGroup ScopeProperties
        {
            get
            {
                return this.scopeProperties;
            }
        }
        
        /// <summary>
        /// Usually the project file name
        /// </summary>
        internal string ScopeName
        {
            get
            {
                return this.scopeName;
            }
        }

        /// <summary>
        /// Usually the version of the toolset used
        /// </summary>
        internal string ScopeToolsVersion
        {
            get
            {
                return this.scopeToolsVersion;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// This method adds an entry to the cache in a thread-safe way
        /// </summary>
        internal void AddCacheEntry(CacheEntry cacheEntry)
        {
            cacheScopeReaderWriterLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                if (cacheEntry != null)
                {
                    AddCacheEntryInternal(cacheEntry);
                }
            }
            finally
            {
                cacheScopeReaderWriterLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// This method adds multiple entries to the cache in a thread-safe way.
        /// </summary>
        /// <param name="cacheEntries"></param>
        internal void AddCacheEntries(CacheEntry[] cacheEntries)
        {
            cacheScopeReaderWriterLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                for (int i = 0; i < cacheEntries.Length; i++)
                {
                    if (cacheEntries[i] != null)
                    {
                        AddCacheEntryInternal(cacheEntries[i]);
                    }
                }
            }
            finally
            {
                cacheScopeReaderWriterLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// This method adds an entry to the cache, without taking a lock
        /// </summary>
        private void AddCacheEntryInternal(CacheEntry cacheEntry)
        {
            if (!cacheContents.ContainsKey(cacheEntry.Name))
            {
                cacheContents.Add(cacheEntry.Name, cacheEntry);
            }
            else
            {
                CacheEntry existingCacheEntry = (CacheEntry)cacheContents[cacheEntry.Name];
                // Make sure the cache values, if overwritten, stay the same. We do not currently support
                // changing the cached value to something else. This allows us to not have a notification
                // mechanism for changed values and if a node has a cached entry it can assume it's up to date.
                // This can change in the future if we discover a compelling scenario for changing cache values.
                if (!cacheEntry.IsEquivalent(existingCacheEntry))
                {
                    ErrorUtilities.VerifyThrowInvalidOperation(false, "CannotModifyCacheEntryValues");
                }
            }
        }

        /// <summary>
        /// This method return the cache entry for a given name. If the cache entry doesn't exist it
        /// return null. This method is thread safe.
        /// </summary>
        internal CacheEntry GetCacheEntry(string name)
        {
            if (cacheContents.ContainsKey(name))
            {
                return (CacheEntry)cacheContents[name];
            }
            
            return null;
        }

        /// <summary>
        /// This method returns the requested set of cache entries. This method is thread safe
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        internal CacheEntry[] GetCacheEntries(string[] names)
        {
            CacheEntry[] results = new CacheEntry[names.Length];

            // This is read only, but since we're processing multiple entries we want to present a consistent
            // view of the cache... we don't want a write between our reads
            cacheScopeReaderWriterLock.AcquireReaderLock(Timeout.Infinite);

            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    results[i] = GetCacheEntry(names[i]);
                }
            }
            finally
            {
                cacheScopeReaderWriterLock.ReleaseReaderLock();
            }

            return results;
        }

        /// <summary>
        /// This method removes an entry from the cache if it exists and does nothing if it doesn't exist
        /// This method is thread safe.
        /// </summary>
        internal void ClearCacheEntry(string name)
        {
            if (cacheContents.ContainsKey(name))
            {
                cacheScopeReaderWriterLock.AcquireWriterLock(Timeout.Infinite);

                try
                {
                    cacheContents.Remove(name);
                }
                finally
                {
                    cacheScopeReaderWriterLock.ReleaseWriterLock();
                }
            }
        }

        /// <summary>
        /// This method returns true if the cache entry for a given name is present in the cache. 
        /// This method is thread safe.
        /// </summary>
        internal bool ContainsCacheEntry(string name)
        {
            return cacheContents.ContainsKey(name);
        }

        /// <summary>
        /// This method adds cached results for each target results for which are contained inside
        /// the build result. This method is thread safe.
        /// </summary>
        internal void AddCacheEntryForBuildResults(BuildResult buildResult)
        {
            ErrorUtilities.VerifyThrow(buildResult != null, "Expect a non-null build result");

            // Don't cache results if they are marked as uncacheable 
            if (!buildResult.UseResultCache)
            {
                return;
            }

            cacheScopeReaderWriterLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                if (!ContainsCacheEntry(Constants.defaultTargetCacheName))
                {
                    // If the project file is malformed the build may fail without initializing the initialtargets or
                    // the default targests fields. The retrieval code expects non-null values
                    // so it is necessary to replace null with empty string
                    ErrorUtilities.VerifyThrow(!buildResult.EvaluationResult || (buildResult.InitialTargets != null 
                                               && buildResult.DefaultTargets != null), 
                                               "Expect initial targets to be non-null for successful builds");
                    string defaultTargets = buildResult.DefaultTargets ?? String.Empty;
                    PropertyCacheEntry defaultTargetsCacheEntry = new PropertyCacheEntry(Constants.defaultTargetCacheName, defaultTargets);
                    AddCacheEntryInternal(defaultTargetsCacheEntry);

                    string initialTargets = buildResult.InitialTargets ?? String.Empty;
                    PropertyCacheEntry initialTargetsCacheEntry = new PropertyCacheEntry(Constants.initialTargetCacheName, initialTargets );
                    AddCacheEntryInternal(initialTargetsCacheEntry);
                }

                if (!ContainsCacheEntry(Constants.projectIdCacheName))
                {
                    PropertyCacheEntry projectIdCacheEntry = new PropertyCacheEntry(Constants.projectIdCacheName, buildResult.ProjectId.ToString(CultureInfo.InvariantCulture));
                    AddCacheEntryInternal(projectIdCacheEntry);
                }

                IDictionary outputsByTargetName = buildResult.OutputsByTarget;

                //Create single entry for each target in the request
                foreach (DictionaryEntry entry in buildResult.ResultByTarget)
                {
                    Target.BuildState buildState = (Target.BuildState)entry.Value;

                    // Only cache successful and failed targets
                    if ((buildState == Target.BuildState.CompletedSuccessfully) ||
                        (buildState == Target.BuildState.CompletedUnsuccessfully))
                    {
                        BuildItem[] targetOutputs = null;

                        // Only cache output items for successful targets
                        if (buildState == Target.BuildState.CompletedSuccessfully)
                        {
                            ErrorUtilities.VerifyThrow(buildResult.OutputsByTarget.Contains(entry.Key),
                                "We must have build results for successful targets");

                            BuildItem[] outputItems = (BuildItem[])buildResult.OutputsByTarget[entry.Key];

                            // It's essential that we clear out any pointers to the project from the BuildItem;
                            // otherwise the cache will hold onto the project, and not save any memory.
                            if (outputItems != null)
                            {
                                for (int i = 0; i < outputItems.Length; i++)
                                {
                                    outputItems[i] = outputItems[i].VirtualClone(true /* remove references to minimise transitive size */);
                                }
                            }

                            targetOutputs = (BuildItem[])buildResult.OutputsByTarget[entry.Key];
                        }

                        BuildResultCacheEntry cacheEntry = new BuildResultCacheEntry((string)entry.Key, targetOutputs,
                            buildState == Target.BuildState.CompletedSuccessfully);

                        if (Engine.debugMode)
                        {
                            Console.WriteLine("+++Adding cache entry for " + (string)entry.Key + " in " +
                                this.ScopeName + " result: " + (buildState == Target.BuildState.CompletedSuccessfully));
                        }

                        AddCacheEntryInternal(cacheEntry);
                    }
                }
            }
            finally
            {
                cacheScopeReaderWriterLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Get a cached build result if available for the given request. This method is thread safe.
        /// </summary>
        /// <param name="buildRequest"></param>
        /// <param name="actuallyBuiltTargets"></param>
        /// <returns></returns>
        internal BuildResult GetCachedBuildResult(BuildRequest buildRequest, out ArrayList actuallyBuiltTargets)
        {
            actuallyBuiltTargets = null;

            PropertyCacheEntry defaultTargetsCacheEntry, initialTargetsCacheEntry, projectIdCacheEntry;

            // No writes here, but since we're reading multiple values we want to get a consistent view of the cache
            cacheScopeReaderWriterLock.AcquireReaderLock(Timeout.Infinite);

            try
            {
                defaultTargetsCacheEntry = (PropertyCacheEntry)GetCacheEntry(Constants.defaultTargetCacheName);
                initialTargetsCacheEntry = (PropertyCacheEntry)GetCacheEntry(Constants.initialTargetCacheName);
                projectIdCacheEntry = (PropertyCacheEntry)GetCacheEntry(Constants.projectIdCacheName);
            }
            finally
            {
                cacheScopeReaderWriterLock.ReleaseReaderLock();
            }

            // If we ever built anything in this project we must have the default and initial targets.
            if (defaultTargetsCacheEntry == null && initialTargetsCacheEntry == null)
            {
                return null;
            }

            ErrorUtilities.VerifyThrow(projectIdCacheEntry != null, "We should always have the projectId cache entry");

            ErrorUtilities.VerifyThrow(defaultTargetsCacheEntry != null && initialTargetsCacheEntry != null,
                "We should have both the initial and default targets in the cache");

            ArrayList targetsToBuild = new ArrayList(initialTargetsCacheEntry.Value.Split(new char[] {';'}, StringSplitOptions.RemoveEmptyEntries));

            if (buildRequest.TargetNames == null || buildRequest.TargetNames.Length == 0)
            {
                targetsToBuild.AddRange(defaultTargetsCacheEntry.Value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                targetsToBuild.AddRange(buildRequest.TargetNames);
            }

            // Create variable to hold the cached outputs
            Hashtable outputsByTargetName = new Hashtable(targetsToBuild.Count);
            Hashtable resultByTarget = new Hashtable(targetsToBuild.Count, StringComparer.OrdinalIgnoreCase);

            bool overallSuccess = true;
            bool missingValues = false;

            // No writes here, but since we're reading multiple values we want to get a consistent view of the cache
            cacheScopeReaderWriterLock.AcquireReaderLock(Timeout.Infinite);

            try
            {
                for (int i = 0; i < targetsToBuild.Count; i++)
                {
                    string targetName = EscapingUtilities.UnescapeAll((string)targetsToBuild[i]);
                    if (ContainsCacheEntry(targetName))
                    {
                        BuildResultCacheEntry cacheEntry = (BuildResultCacheEntry)GetCacheEntry(targetName);
                        overallSuccess = overallSuccess && cacheEntry.BuildResult;
                        resultByTarget[targetName] = (cacheEntry.BuildResult) ?
                            Target.BuildState.CompletedSuccessfully : Target.BuildState.CompletedUnsuccessfully;

                        // Restore output items for successful targets
                        if (cacheEntry.BuildResult)
                        {
                            outputsByTargetName[targetName] = cacheEntry.BuildItems;
                        }
                        // We found a failed target - cut the loop short
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        missingValues = true;
                        break;
                    }
                }
            }
            finally
            {
                cacheScopeReaderWriterLock.ReleaseReaderLock();
            }

            if (missingValues)
            {
                return null;
            }

            actuallyBuiltTargets = targetsToBuild;

            return new BuildResult(outputsByTargetName, resultByTarget, overallSuccess, buildRequest.HandleId, buildRequest.RequestId, 
                int.Parse(projectIdCacheEntry.Value, CultureInfo.InvariantCulture), false /* use results cache */, 
                defaultTargetsCacheEntry.Value, initialTargetsCacheEntry.Value, 0, 0, 0);
        }

        #endregion

        #region Data
        // This is normally the name of the project file to which the cached entries refer
        private string scopeName;
        // The version of the toolset the project uses
        private string scopeToolsVersion;
        // This is normally a set of properties for the project file to which the cached entries refer
        private BuildPropertyGroup scopeProperties;
        // This dictionary contains all the cached items within the current scope
        private Hashtable cacheContents;
        // Synchronization between multiple threads
        private static ReaderWriterLock cacheScopeReaderWriterLock = new ReaderWriterLock();
        #endregion
    }
}
