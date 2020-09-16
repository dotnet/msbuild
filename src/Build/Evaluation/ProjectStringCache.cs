// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// This class will cache string values for loaded Xml files.
    /// </summary>
    [DebuggerDisplay("#Strings={Count} #Documents={_documents.Count}")]
    internal class ProjectStringCache
    {
        /// <summary>
        /// Start off with a large size as there are very many strings in common scenarios and resizing is expensive.
        /// Note that there is a single instance of this cache for the lifetime of the process (albeit cleared out on XML unload)
        /// Australian Govt has about 3000 strings; a single VC project with all its various XML files has about 4000 strings.
        /// </summary>
        private const int InitialSize = 5000;

        /// <summary>
        /// Store interned strings, and also a ref count, one per document using them.
        /// </summary>
        private RetrievableEntryHashSet<StringCacheEntry> _strings = new RetrievableEntryHashSet<StringCacheEntry>(InitialSize, StringComparer.Ordinal);

        /// <summary>
        /// Store all the strings a document is using, so their ref count can be decremented.
        /// </summary>
        private Dictionary<XmlDocument, HashSet<StringCacheEntry>> _documents = new Dictionary<XmlDocument, HashSet<StringCacheEntry>>();

        /// <summary>
        /// Locking object for this shared cache
        /// </summary>
        private Object _locker = new Object();

        /// <summary>
        /// Public constructor.
        /// </summary>
        public ProjectStringCache()
        {
            ProjectRootElementCacheBase.StrongCacheEntryRemoved += OnStrongCacheEntryRemoved;
        }

        /// <summary>
        /// Obtain the number of entries contained in the cache.
        /// </summary>
        internal int Count
        {
            get
            {
                lock (_locker)
                {
                    return _strings.Count;
                }
            }
        }

        /// <summary>
        /// Add the given string to the cache or return the existing string if it is already
        /// in the cache.
        /// Constant time operation.
        /// </summary>
        public string Add(string key, XmlDocument document)
        {
            if (key.Length == 0)
            {
                return String.Empty;
            }

            // see Microsoft.Build.BackEnd.BuildRequestConfiguration.CreateUniqueGlobalProperty
            if (key.StartsWith(MSBuildConstants.MSBuildDummyGlobalPropertyHeader, StringComparison.Ordinal))
            {
                return key;
            }

            lock (_locker)
            {
                VerifyState();

                StringCacheEntry entry;
                HashSet<StringCacheEntry> entries;

                bool seenString = _strings.TryGetValue(key, out entry);
                bool seenDocument = _documents.TryGetValue(document, out entries);

                if (!seenString)
                {
                    entry = new StringCacheEntry(key);
                    _strings.Add(entry);
                }

                if (!seenDocument)
                {
                    entries = new HashSet<StringCacheEntry>();
                    _documents.Add(document, entries);
                }

                bool seenStringInThisDocument = seenString && seenDocument && entries.Contains(entry);

                if (!seenStringInThisDocument)
                {
                    entries.Add(entry);

                    // We've been referred to by a new document, so increment our ref count.
                    entry.Increment();
                }

                VerifyState();

                return entry.CachedString;
            }
        }

        /// <summary>
        /// Find the matching string in the cache.
        /// Constant time operation.
        /// </summary>
        /// <param name="key">String to find in the cache.</param>
        /// <returns>Existing string in the cache, or null if it is not contained.</returns>
        public string Get(string key)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentNull(key, nameof(key));

                if (key.Length == 0)
                {
                    return String.Empty;
                }

                StringCacheEntry entry;
                if (_strings.TryGetValue(key, out entry))
                {
                    return entry.CachedString;
                }

                return null;
            }
        }

        /// <summary>
        /// Indicates that a document's entries should be removed.
        /// If document is unknown, does nothing.
        /// Complexity proportional to the number of strings in the document,
        /// if the document is anywhere in the cache, otherwise O(1).
        /// </summary>
        public void Clear(XmlDocument document)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentNull(document, nameof(document));

                VerifyState();

                HashSet<StringCacheEntry> entries;
                if (_documents.TryGetValue(document, out entries))
                {
                    foreach (var entry in entries)
                    {
                        string str = entry.CachedString;
                        entry.Decrement();

                        if (entry.RefCount == 0)
                        {
                            _strings.Remove(str);
                        }
                    }

                    _documents.Remove(document);
                }

                VerifyState();
            }
        }

        /// <summary>
        /// Verifies that each string entry has only one instance in the system.
        /// Enable the conditional if and while you make any modifications to the class, then disable as it is very slow.
        /// </summary>
        [Conditional("NEVER")]
        private void VerifyState()
        {
            HashSet<StringCacheEntry> uniqueEntries = new HashSet<StringCacheEntry>();
            foreach (var entries in _documents.Values)
            {
                foreach (var entry in entries)
                {
                    uniqueEntries.Add(entry);
                    ErrorUtilities.VerifyThrow(entry.RefCount > 0, "extra deref");

                    // We only ever create one StringCacheEntry instance per unique string, and that instance should be 
                    // the same in both collections.
                    ErrorUtilities.VerifyThrow(Object.ReferenceEquals(entry, _strings[entry.CachedString]), "bad state");
                }
            }

            ErrorUtilities.VerifyThrow(uniqueEntries.Count == _strings.Count, "bad state");
        }

        /// <summary>
        /// Handle event that is fired when an entry in the project root element cache is removed 
        /// from its strong cache.
        /// </summary>
        /// <remarks>
        /// When an entry is removed from a project root element cache's strong cache, we will remove
        /// its entries from our string cache. Otherwise the string cache ends up being the only one
        /// holding references to the Xml documents that have already been dropped.
        /// </remarks>
        private void OnStrongCacheEntryRemoved(object sender, ProjectRootElement projectRootElement)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectRootElement, nameof(projectRootElement));
            Clear(projectRootElement.XmlDocument);
        }

        /// <summary>
        /// Represents an entry in the ProjectStringCache.
        /// Can't be a struct because the copy-by-value and the ref counting don't go well together.
        /// </summary>
        [DebuggerDisplay("Count={_refCount} String={_cachedString}")]
        private class StringCacheEntry : IKeyed
        {
            /// <summary>
            /// Cached string
            /// </summary>
            private string _cachedString;

            /// <summary>
            /// Number of XmlDocuments where this string is included.
            /// </summary>
            private int _refCount;

            /// <summary>
            /// Constructor.
            /// Caller must then do Increment().
            /// </summary>
            internal StringCacheEntry(string str)
            {
                _cachedString = str;
                _refCount = 0;
            }

            /// <summary>
            /// Key to find it
            /// </summary>
            public string Key
            {
                get { return _cachedString; }
            }

            /// <summary>
            /// Number of documents using this string
            /// </summary>
            internal int RefCount
            {
                get { return _refCount; }
            }

            /// <summary>
            /// Get the cached string.
            /// </summary>
            internal string CachedString
            {
                get
                {
                    ErrorUtilities.VerifyThrow(_refCount > 0, "extra deref");
                    return _cachedString;
                }
            }

            /// <summary>
            /// Indicates that this entry is included in the given document.
            /// Callers must verify that we were not already adreffed for this document.
            /// </summary>
            internal void Increment()
            {
                _refCount++;
            }

            /// <summary>
            /// Removes a container for this entry.
            /// Callers must verify that this was not already reffed and not subsequently dereffed.
            /// </summary>
            internal void Decrement()
            {
                ErrorUtilities.VerifyThrow(_refCount > 0, "extra deref");
                _refCount--;
            }
        }
    }
}
