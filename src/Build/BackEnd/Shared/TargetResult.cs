// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.IO.Compression;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Contains the result items for a single target as well as the overall result code.
    /// </summary>
    public class TargetResult : ITargetResult, INodePacketTranslatable
    {
        /// <summary>
        /// The result for this target.
        /// </summary>
        private WorkUnitResult _result;

        /// <summary>
        /// Flag indicating whether to consider this target failure as having caused a build failure.
        /// </summary>
        private bool _targetFailureDoesntCauseBuildFailure;

        /// <summary>
        /// The store of items in this result.
        /// </summary>
        private ItemsStore _itemsStore;

        /// <summary>
        /// The context under which these results have been cached.
        /// </summary>
        private CacheInfo _cacheInfo;

        /// <summary>
        /// Initializes the results with specified items and result.
        /// </summary>
        /// <param name="items">The items produced by the target.</param>
        /// <param name="result">The overall result for the target.</param>
        internal TargetResult(TaskItem[] items, WorkUnitResult result)
        {
            ErrorUtilities.VerifyThrowArgumentNull(items, nameof(items));
            ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));
            _itemsStore = new ItemsStore(items);
            _result = result;
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private TargetResult(INodePacketTranslator translator)
        {
            ((INodePacketTranslatable)this).Translate(translator);
        }

        /// <summary>
        /// Returns the exception which aborted this target, if any.
        /// </summary>
        /// <value>The exception which aborted this target, if any.</value>
        public Exception Exception {
            [DebuggerStepThrough]
            get => _result.Exception;
        }

        /// <summary>
        /// Returns the items produced by the target.
        /// These are ITaskItem's, so they have no item-type.
        /// </summary>
        /// <value>The items produced by the target.</value>
        public ITaskItem[] Items
        {
            [DebuggerStepThrough]
            get
            {
                lock (_result)
                {
                    RetrieveItemsFromCache();

                    // NOTE: If the items in the ItemsStore were compressed, this will decompress them.  If the only purpose to
                    // getting these items is to check the length, then that is inefficient and we should come up with a better
                    // way of getting the count (such as another interface method which delegates to the ItemsStore itself.
                    return _itemsStore.Items;
                }
            }
        }

        /// <summary>
        /// Returns the result code for the target.
        /// </summary>
        /// <value>The result code for the target.</value>
        public TargetResultCode ResultCode
        {
            [DebuggerStepThrough]
            get
            {
                switch (_result.ResultCode)
                {
                    case WorkUnitResultCode.Canceled:
                    case WorkUnitResultCode.Failed:
                        return TargetResultCode.Failure;

                    case WorkUnitResultCode.Skipped:
                        return TargetResultCode.Skipped;

                    case WorkUnitResultCode.Success:
                        return TargetResultCode.Success;

                    default:
                        return TargetResultCode.Skipped;
                }
            }
        }

        /// <summary>
        /// Returns the internal result for the target.
        /// </summary>
        internal WorkUnitResult WorkUnitResult
        {
            [DebuggerStepThrough]
            get => _result;
        }

        /// <summary>
        /// Sets or gets a flag indicating whether or not a failure results should cause the build to fail.
        /// </summary>
        internal bool TargetFailureDoesntCauseBuildFailure
        {
            [DebuggerStepThrough]
            get => _targetFailureDoesntCauseBuildFailure;

            [DebuggerStepThrough]
            set => _targetFailureDoesntCauseBuildFailure = value;
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                lock (_result)
                {
                    // Should we have cached these items but now want to send them to another node, we need to 
                    // ensure they are loaded before doing so.
                    RetrieveItemsFromCache();
                    InternalTranslate(translator);
                }
            }
            else
            {
                InternalTranslate(translator);
            }
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static TargetResult FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new TargetResult(translator);
        }

        #endregion

        /// <summary>
        /// Gets the name of the cache file for this configuration.
        /// </summary>
        internal static string GetCacheFile(int configId, string targetToCache)
        {
            string filename = Path.Combine(FileUtilities.GetCacheDirectory(), String.Format(CultureInfo.InvariantCulture, Path.Combine("Results{0}", "{1}.cache"), configId, targetToCache));
            return filename;
        }

        /// <summary>
        /// Gets the name of the cache file for this configuration.
        /// </summary>
        internal static string GetCacheDirectory(int configId, string targetToCache)
        {
            string filename = GetCacheFile(configId, targetToCache);
            string directoryName = Path.GetDirectoryName(filename);
            return directoryName;
        }

        /// <summary>
        /// Cache the items.
        /// </summary>
        internal void CacheItems(int configId, string targetName)
        {
            lock (_result)
            {
                if (_itemsStore == null)
                {
                    // Already cached.
                    return;
                }

                if (_itemsStore.ItemsCount == 0)
                {
                    // Nothing to cache.
                    return;
                }

                INodePacketTranslator translator = GetResultsCacheTranslator(configId, targetName, TranslationDirection.WriteToStream);

                // If the translator is null, it means these results were cached once before.  Since target results are immutable once they
                // have been created, there is no point in writing them again.
                if (translator != null)
                {
                    try
                    {
                        translator.Translate(ref _itemsStore, ItemsStore.FactoryForDeserialization);
                        _itemsStore = null;
                        _cacheInfo = new CacheInfo(configId, targetName);
                    }
                    finally
                    {
                        translator.Writer.BaseStream.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Performs the actual translation
        /// </summary>
        private void InternalTranslate(INodePacketTranslator translator)
        {
            translator.Translate(ref _result, WorkUnitResult.FactoryForDeserialization);
            translator.Translate(ref _targetFailureDoesntCauseBuildFailure);
            translator.Translate(ref _itemsStore, ItemsStore.FactoryForDeserialization);
        }

        /// <summary>
        /// Retrieve the items from the cache.
        /// </summary>
        private void RetrieveItemsFromCache()
        {
            lock (_result)
            {
                if (_itemsStore == null)
                {
                    INodePacketTranslator translator = GetResultsCacheTranslator(_cacheInfo.ConfigId, _cacheInfo.TargetName, TranslationDirection.ReadFromStream);

                    try
                    {
                        translator.Translate(ref _itemsStore, ItemsStore.FactoryForDeserialization);
                        _cacheInfo = new CacheInfo();
                    }
                    finally
                    {
                        translator.Reader.BaseStream.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the translator for this configuration.
        /// </summary>
        private static INodePacketTranslator GetResultsCacheTranslator(int configId, string targetToCache, TranslationDirection direction)
        {
            string cacheFile = GetCacheFile(configId, targetToCache);
            if (direction == TranslationDirection.WriteToStream)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                if (FileSystems.Default.FileExists(cacheFile))
                {
                    // If the file already exists, then we have cached this once before.  No need to cache it again since it cannot have changed.
                    return null;
                }

                return NodePacketTranslator.GetWriteTranslator(File.Create(cacheFile));
            }
            else
            {
                return NodePacketTranslator.GetReadTranslator(File.OpenRead(cacheFile), null);
            }
        }

        /// <summary>
        /// Information about where the cache for the items in this result are stored.
        /// </summary>
        private struct CacheInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public CacheInfo(int configId, string targetName)
            {
                ConfigId = configId;
                TargetName = targetName;
            }

            /// <summary>
            /// Retrieves the configuration id.
            /// </summary>
            public int ConfigId { get; }

            /// <summary>
            /// Retrieves the target name.
            /// </summary>
            public string TargetName { get; }
        }

        /// <summary>
        /// The store of items for the target result.  This class is responsible for the serialization of the items collection, which is 
        /// useful to keep separate as it is where we spend most of our time serializing for large projects, and these are the bits
        /// we throw out of memory when the cache gets collected.
        /// </summary>
        private class ItemsStore : INodePacketTranslatable
        {
            /// <summary>
            /// The default compression threshold.
            /// </summary>
            private const int DefaultCompressionThreshold = 32;

            /// <summary>
            /// The count of items we will store before we start using compression.
            /// </summary>
            /// <remarks>
            /// This value was determined empirically by looking at how many items tend to be transmitted for "normal" projects versus the ones
            /// which benefit from this technique.
            /// </remarks>
            private static readonly int s_compressionThreshold;

            /// <summary>
            /// The compressed set of items, if any.
            /// </summary>
            private byte[] _compressedItems;

            /// <summary>
            /// The count of items, stored here so that we don't have to decompress the items if we are
            /// only looking at the count.
            /// </summary>
            private int _itemsCount;

            /// <summary>
            /// The items produced by this target.
            /// </summary>
            private TaskItem[] _uncompressedItems;

            /// <summary>
            /// Static constructor.
            /// </summary>
            static ItemsStore()
            {
                if (Int32.TryParse(Environment.GetEnvironmentVariable("MSBUILDTARGETRESULTCOMPRESSIONTHRESHOLD"), out ItemsStore.s_compressionThreshold))
                {
                    if (s_compressionThreshold < 0)
                    {
                        s_compressionThreshold = 0;
                    }
                }
                else
                {
                    s_compressionThreshold = DefaultCompressionThreshold;
                }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            public ItemsStore(TaskItem[] items)
            {
                ErrorUtilities.VerifyThrowArgumentNull(items, "items");
                _uncompressedItems = items;
                _itemsCount = items.Length;
            }

            /// <summary>
            /// Constructor for serialization.
            /// </summary>
            private ItemsStore(INodePacketTranslator translator)
            {
                Translate(translator);
            }

            /// <summary>
            /// Gets the count of items.
            /// </summary>
            public int ItemsCount => _itemsCount;

            /// <summary>
            /// Retrieves the items.
            /// </summary>
            /// <remarks>
            /// It's important not to call this method merely to get a count of the items held in the collection.
            /// Instead use ItemsCount (above) for that.
            /// </remarks>
            public TaskItem[] Items
            {
                get
                {
                    if (_uncompressedItems == null)
                    {
                        DecompressItems();
                    }

                    return _uncompressedItems;
                }
            }

            /// <summary>
            /// Throws out the deserialized items.
            /// </summary>
            /// <remarks>
            /// Not presently used, but could be used for a multi-stage caching mechanism which first throws out decompressed items,
            /// then if more space is needed, starts throwing out the compressed ones.
            /// </remarks>
            [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Keeping around so that we can potentially expand on our current caching mechanism later")]
            public void ReleaseItems()
            {
                if (_compressedItems == null)
                {
                    CompressItems();
                }
            }

            /// <summary>
            /// Translates an items store.
            /// </summary>
            public void Translate(INodePacketTranslator translator)
            {
                if (_compressedItems == null && translator.Mode == TranslationDirection.WriteToStream)
                {
                    CompressItemsIfNecessary();
                }

                // Note we only translate the serialized buffer (which contains the compressed and interned
                // representation of the items.)  If the actual items are needed (for instance on child nodes)
                // then the Items accessor will reconstitute them at the point they are needed.
                ErrorUtilities.VerifyThrow((translator.Mode == TranslationDirection.ReadFromStream) || ((_compressedItems == null) ^ (_uncompressedItems == null)), "One of the compressed or uncompressed items arrays should be null.");
                translator.Translate(ref _itemsCount);
                translator.Translate(ref _compressedItems);
                translator.TranslateArray(ref _uncompressedItems, TaskItem.FactoryForDeserialization);
            }

            /// <summary>
            /// Factory for the serializer.
            /// </summary>
            internal static ItemsStore FactoryForDeserialization(INodePacketTranslator translator)
            {
                return new ItemsStore(translator);
            }

            /// <summary>
            /// Compresses the items, but only if we have reached the threshold where it makes sense to do so.
            /// </summary>
            private void CompressItemsIfNecessary()
            {
                if (_itemsCount > s_compressionThreshold)
                {
                    CompressItems();
                }
            }

            /// <summary>
            /// Decompresses the items.
            /// </summary>
            private void DecompressItems()
            {
                ErrorUtilities.VerifyThrow(_uncompressedItems == null, "Items already decompressed.");
                using (MemoryStream serializedStream = new MemoryStream(_compressedItems, 0, _compressedItems.Length, writable: false, publiclyVisible: true))
                {
                    using (DeflateStream inflateStream = new DeflateStream(serializedStream, CompressionMode.Decompress))
                    {
                        INodePacketTranslator serializedBufferTranslator = NodePacketTranslator.GetReadTranslator(inflateStream, null);
                        LookasideStringInterner interner = new LookasideStringInterner(serializedBufferTranslator);

                        byte[] buffer = null;
                        serializedBufferTranslator.Translate(ref buffer);
                        ErrorUtilities.VerifyThrow(buffer != null, "Unexpected null items buffer during decompression.");

                        using (MemoryStream itemsStream = new MemoryStream(buffer, 0, buffer.Length, writable: false, publiclyVisible: true))
                        {
                            INodePacketTranslator itemTranslator = NodePacketTranslator.GetReadTranslator(itemsStream, null);
                            _uncompressedItems = new TaskItem[_itemsCount];
                            for (int i = 0; i < _uncompressedItems.Length; i++)
                            {
                                _uncompressedItems[i] = TaskItem.FactoryForDeserialization(itemTranslator, interner);
                            }
                        }
                    }
                }

                _compressedItems = null;
            }

            /// <summary>
            /// Compresses the items.
            /// </summary>
            private void CompressItems()
            {
                ErrorUtilities.VerifyThrow(_compressedItems == null, "Items already compressed.");

                // We will just calculate a very rough starting buffer size for the memory stream based on the number of items and a
                // rough guess for an average number of bytes needed to store them compressed.  This doesn't have to be accurate, just
                // big enough to avoid unnecessary buffer reallocations in most cases.
                int defaultCompressedBufferCapacity = _uncompressedItems.Length * 64;
                using (var serializedStream = new MemoryStream(defaultCompressedBufferCapacity))
                {
                    using (var deflateStream = new DeflateStream(serializedStream, CompressionMode.Compress))
                    {
                        INodePacketTranslator serializedBufferTranslator = NodePacketTranslator.GetWriteTranslator(deflateStream);

                        // Again, a rough calculation of buffer size, this time for an uncompressed buffer.  We assume compression 
                        // will give us 2:1, as it's all text.
                        int defaultUncompressedBufferCapacity = defaultCompressedBufferCapacity * 2;
                        using (var itemsStream = new MemoryStream(defaultUncompressedBufferCapacity))
                        {
                            INodePacketTranslator itemTranslator = NodePacketTranslator.GetWriteTranslator(itemsStream);

                            // When creating the interner, we use the number of items as the initial size of the collections since the
                            // number of strings will be of the order of the number of items in the collection.  This assumes basically
                            // one unique string per item (frequently a path related to the item) with most of the rest of the metadata
                            // being the same (and thus interning.)  This is a hueristic meant to get us in the ballpark to avoid 
                            // too many reallocations when growing the collections.
                            LookasideStringInterner interner = new LookasideStringInterner(StringComparer.Ordinal, _uncompressedItems.Length);
                            foreach (TaskItem t in _uncompressedItems)
                            {
                                t.TranslateWithInterning(itemTranslator, interner);
                            }

                            interner.Translate(serializedBufferTranslator);
                            byte[] buffer = itemsStream.ToArray();
                            serializedBufferTranslator.Translate(ref buffer);
                        }
                    }

                    _compressedItems = serializedStream.ToArray();
                }

                _uncompressedItems = null;
            }
        }
    }
}
