// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// A strongly-typed wrapper around <see cref="ITaskItem"/> that parses the item's identity
    /// as a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to parse the item identity as. Must be a value type.</typeparam>
    /// <remarks>
    /// This allows tasks to receive strongly-typed parameters while still working with MSBuild's item system.
    /// The identity (ItemSpec) is parsed using <see cref="Convert.ChangeType(object?, Type)"/> or special handling for
    /// types like <see cref="AbsolutePath"/>.
    /// </remarks>
    public readonly struct TaskItem<T> : ITaskItem<T>, IEquatable<TaskItem<T>>
        where T : struct
    {
        private readonly ITaskItem _backingItem;

        /// <summary>
        /// Gets the strongly-typed value parsed from the item's identity.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="TaskItem{T}"/> from a strongly-typed value.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        public TaskItem(T value)
        {
            Value = value;

            // Create a backing item with the stringified value as ItemSpec
            string itemSpec = ValueTypeParser.ToString(value);
            _backingItem = new TaskItemData(itemSpec, null);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TaskItem{T}"/> from an <see cref="ITaskItem"/>.
        /// </summary>
        /// <param name="item">The task item to parse.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="item"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the item's identity cannot be parsed as type <typeparamref name="T"/>.</exception>
        public TaskItem(ITaskItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            _backingItem = item;

            string itemSpec = item.ItemSpec;
            if (string.IsNullOrEmpty(itemSpec))
            {
                throw new ArgumentException($"Cannot create TaskItem<{typeof(T).Name}> from an item with empty identity.", nameof(item));
            }

            // Parse the value from ItemSpec
            Value = ParseValue(itemSpec);
        }

        /// <summary>
        /// Parses a string value as type <typeparamref name="T"/>.
        /// Uses the unified ValueTypeParser for consistent parsing behavior across MSBuild.
        /// </summary>
        private static T ParseValue(string value) => (T)ValueTypeParser.Parse(value, typeof(T));

        /// <summary>
        /// Converts the strongly-typed value to its string representation for use as ItemSpec.
        /// Uses the unified ValueTypeParser for consistent formatting behavior across MSBuild.
        /// </summary>
        private string ValueToString() => ValueTypeParser.ToString(Value);

        #region ITaskItem Implementation

        /// <inheritdoc/>
        public string ItemSpec
        {
            get => _backingItem.ItemSpec;
            set => throw new NotSupportedException("TaskItem<T> ItemSpec is read-only. Create a new instance to change the value.");
        }

        /// <inheritdoc/>
        public ICollection MetadataNames => _backingItem.MetadataNames;

        /// <inheritdoc/>
        public int MetadataCount => _backingItem.MetadataCount;

        /// <inheritdoc/>
        public string GetMetadata(string metadataName) => _backingItem.GetMetadata(metadataName);

        /// <inheritdoc/>
        public void SetMetadata(string metadataName, string metadataValue) =>
            _backingItem.SetMetadata(metadataName, metadataValue);

        /// <inheritdoc/>
        public void RemoveMetadata(string metadataName) =>
            _backingItem.RemoveMetadata(metadataName);

        /// <inheritdoc/>
        public void CopyMetadataTo(ITaskItem destinationItem) =>
            _backingItem.CopyMetadataTo(destinationItem);

        /// <inheritdoc/>
        public IDictionary CloneCustomMetadata() => _backingItem.CloneCustomMetadata();

        #endregion

        #region ITaskItem2 Implementation

        /// <inheritdoc/>
        public string EvaluatedIncludeEscaped
        {
            get => (_backingItem as ITaskItem2)?.EvaluatedIncludeEscaped ?? ItemSpec;
            set => throw new NotSupportedException("TaskItem<T> EvaluatedIncludeEscaped is read-only. Create a new instance to change the value.");
        }

        /// <inheritdoc/>
        public string GetMetadataValueEscaped(string metadataName)
        {
            return (_backingItem as ITaskItem2)?.GetMetadataValueEscaped(metadataName) ?? string.Empty;
        }

        /// <inheritdoc/>
        public void SetMetadataValueLiteral(string metadataName, string metadataValue)
        {
            if (_backingItem is ITaskItem2 taskItem2)
            {
                taskItem2.SetMetadataValueLiteral(metadataName, metadataValue);
            }
            else
            {
                // For ITaskItem (non-ITaskItem2), we need to escape the value manually
                // Since we don't have access to EscapingUtilities in Framework, we'll just set it directly
                // This is acceptable because TaskItem<T> is primarily used with ITaskItem2 implementations
                _backingItem.SetMetadata(metadataName, metadataValue);
            }
        }

        /// <inheritdoc/>
        public IDictionary CloneCustomMetadataEscaped()
        {
            return (_backingItem as ITaskItem2)?.CloneCustomMetadataEscaped() ?? new Hashtable();
        }

        #endregion

        #region Equality and Conversion

        /// <inheritdoc/>
        public bool Equals(TaskItem<T> other) => Value.Equals(other.Value);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is TaskItem<T> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Determines whether two <see cref="TaskItem{T}"/> instances are equal.
        /// </summary>
        public static bool operator ==(TaskItem<T> left, TaskItem<T> right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="TaskItem{T}"/> instances are not equal.
        /// </summary>
        public static bool operator !=(TaskItem<T> left, TaskItem<T> right) => !left.Equals(right);

        /// <summary>
        /// Implicitly converts a <see cref="TaskItem{T}"/> to its strongly-typed value.
        /// </summary>
        public static implicit operator T(TaskItem<T> taskItem) => taskItem.Value;

        /// <summary>
        /// Implicitly converts a strongly-typed value to a <see cref="TaskItem{T}"/>.
        /// </summary>
        public static implicit operator TaskItem<T>(T value) => new TaskItem<T>(value);

        #endregion

        /// <inheritdoc/>
        public override string ToString() => ValueToString();
    }
}
