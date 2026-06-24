// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Engine-internal concrete implementation of <see cref="ITaskItem{T}"/> used to materialize strongly-typed
    /// task item parameters during parameter binding.
    /// </summary>
    /// <remarks>
    /// This mirrors the public <c>Microsoft.Build.Utilities.TaskItem&lt;T&gt;</c>, but lives in the engine so that
    /// <c>Microsoft.Build</c> does not need a build/package dependency on <c>Microsoft.Build.Utilities.Core</c>.
    /// Identity parsing is delegated to <see cref="ValueTypeParser"/> so behavior stays consistent with the public
    /// type.
    /// </remarks>
    /// <typeparam name="T">The type to parse the item identity as (<see cref="AbsolutePath"/>, <see cref="FileInfo"/>, or <see cref="DirectoryInfo"/>).</typeparam>
    internal sealed class StronglyTypedTaskItem<T> : ITaskItem<T>
    {
        private readonly ITaskItem _backingItem;

        /// <summary>
        /// Initializes a new instance from an <see cref="ITaskItem"/>, parsing its identity as <typeparamref name="T"/>.
        /// </summary>
        /// <param name="item">The task item whose identity is parsed.</param>
        internal StronglyTypedTaskItem(ITaskItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            _backingItem = item;

            string itemSpec = item.ItemSpec;
            if (string.IsNullOrEmpty(itemSpec))
            {
                throw new ArgumentException($"Cannot create ITaskItem<{typeof(T).Name}> from an item with empty identity.", nameof(item));
            }

            // For path-like types, prefer the FullPath metadata (which is always absolute) over the ItemSpec,
            // which may be relative.
            string parseFrom = IsPathLikeType() ? GetFullPathOrItemSpec(item, itemSpec) : itemSpec;
            Value = (T)ValueTypeParser.Parse(parseFrom, typeof(T));
        }

        /// <inheritdoc/>
        public T Value { get; }

        /// <summary>Returns true if T is a path-like type that benefits from absolute path resolution.</summary>
        private static bool IsPathLikeType()
        {
            Type t = typeof(T);
            return t == typeof(FileInfo) || t == typeof(DirectoryInfo) || t == typeof(AbsolutePath);
        }

        /// <summary>Returns the FullPath metadata value if non-empty, otherwise falls back to itemSpec.</summary>
        private static string GetFullPathOrItemSpec(ITaskItem item, string itemSpec)
        {
            string fullPath = item.GetMetadata("FullPath");
            return !string.IsNullOrEmpty(fullPath) ? fullPath : itemSpec;
        }

        #region ITaskItem Implementation

        /// <inheritdoc/>
        public string ItemSpec
        {
            get => _backingItem.ItemSpec;
            set => throw new NotSupportedException("ItemSpec is read-only on a strongly-typed task item. Create a new instance to change the value.");
        }

        /// <inheritdoc/>
        public ICollection MetadataNames => _backingItem.MetadataNames;

        /// <inheritdoc/>
        public int MetadataCount => _backingItem.MetadataCount;

        /// <inheritdoc/>
        public string GetMetadata(string metadataName) => _backingItem.GetMetadata(metadataName);

        /// <inheritdoc/>
        public void SetMetadata(string metadataName, string metadataValue) => _backingItem.SetMetadata(metadataName, metadataValue);

        /// <inheritdoc/>
        public void RemoveMetadata(string metadataName) => _backingItem.RemoveMetadata(metadataName);

        /// <inheritdoc/>
        public void CopyMetadataTo(ITaskItem destinationItem) => _backingItem.CopyMetadataTo(destinationItem);

        /// <inheritdoc/>
        public IDictionary CloneCustomMetadata() => _backingItem.CloneCustomMetadata();

        #endregion

        #region ITaskItem2 Implementation

        /// <inheritdoc/>
        public string EvaluatedIncludeEscaped
        {
            get => (_backingItem as ITaskItem2)?.EvaluatedIncludeEscaped ?? _backingItem.ItemSpec;
            set => throw new NotSupportedException("EvaluatedIncludeEscaped is read-only on a strongly-typed task item. Create a new instance to change the value.");
        }

        /// <inheritdoc/>
        public string GetMetadataValueEscaped(string metadataName) =>
            (_backingItem as ITaskItem2)?.GetMetadataValueEscaped(metadataName) ?? _backingItem.GetMetadata(metadataName);

        /// <inheritdoc/>
        public void SetMetadataValueLiteral(string metadataName, string metadataValue)
        {
            if (_backingItem is ITaskItem2 taskItem2)
            {
                taskItem2.SetMetadataValueLiteral(metadataName, metadataValue);
            }
            else
            {
                _backingItem.SetMetadata(metadataName, EscapingUtilities.Escape(metadataValue));
            }
        }

        /// <inheritdoc/>
        public IDictionary CloneCustomMetadataEscaped() =>
            (_backingItem as ITaskItem2)?.CloneCustomMetadataEscaped() ?? _backingItem.CloneCustomMetadata();

        #endregion
    }
}
