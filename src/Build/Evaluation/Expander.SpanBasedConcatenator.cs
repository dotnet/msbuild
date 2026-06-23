// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// A helper struct wrapping a <see cref="SpanBasedStringBuilder"/> and providing file path conversion
    /// as used in e.g. property expansion.
    /// </summary>
    /// <remarks>
    /// If exactly one value is added and no concatenation takes places, this value is returned without
    /// conversion. In other cases values are stringified and attempted to be interpreted as file paths
    /// before concatenation.
    /// </remarks>
    private struct SpanBasedConcatenator : IDisposable
    {
        /// <summary>
        /// The backing <see cref="SpanBasedStringBuilder"/>, null until the second value is added.
        /// </summary>
        private SpanBasedStringBuilder _builder;

        /// <summary>
        /// The first value added to the concatenator. Tracked in its own field so it can be returned
        /// without conversion if no concatenation takes place.
        /// </summary>
        private object _firstObject;

        /// <summary>
        /// The first value added to the concatenator if it is a span. Tracked in its own field so the
        /// <see cref="SpanBasedStringBuilder"/> functionality doesn't have to be invoked if no concatenation
        /// takes place.
        /// </summary>
        private ReadOnlyMemory<char> _firstSpan;

        /// <summary>
        /// True if this instance is already disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Adds an object to be concatenated.
        /// </summary>
        public void Add(object obj)
        {
            CheckDisposed();
            FlushFirstValueIfNeeded();
            if (_builder != null)
            {
                _builder.Append(FileUtilities.MaybeAdjustFilePath(obj.ToString()));
            }
            else
            {
                _firstObject = obj;
            }
        }

        /// <summary>
        /// Adds a span to be concatenated.
        /// </summary>
        public void Add(ReadOnlyMemory<char> span)
        {
            CheckDisposed();
            FlushFirstValueIfNeeded();
            if (_builder != null)
            {
                _builder.Append(FileUtilities.MaybeAdjustFilePath(span));
            }
            else
            {
                _firstSpan = span;
            }
        }

        /// <summary>
        /// Returns the result of the concatenation.
        /// </summary>
        /// <returns>
        /// If only one value has been added and it is not a string, it is returned unchanged.
        /// In all other cases (no value, one string value, multiple values) the result is a
        /// concatenation of the string representation of the values, each additionally subjected
        /// to file path adjustment.
        /// </returns>
        public readonly object GetResult()
        {
            CheckDisposed();
            if (_firstObject != null)
            {
                return (_firstObject is string stringValue) ? FileUtilities.MaybeAdjustFilePath(stringValue) : _firstObject;
            }
            return _firstSpan.IsEmpty
                ? _builder?.ToString() ?? string.Empty
                : FileUtilities.MaybeAdjustFilePath(_firstSpan).ToString();
        }

        /// <summary>
        /// Disposes of the struct by delegating the call to the underlying <see cref="SpanBasedStringBuilder"/>.
        /// </summary>
        public void Dispose()
        {
            CheckDisposed();
            _builder?.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if this concatenator is already disposed.
        /// </summary>
        private readonly void CheckDisposed() =>
            ObjectDisposedException.ThrowIf(_disposed, typeof(SpanBasedConcatenator));

        /// <summary>
        /// Lazily initializes <see cref="_builder"/> and populates it with the first value
        /// when the second value is being added.
        /// </summary>
        private void FlushFirstValueIfNeeded()
        {
            if (_firstObject != null)
            {
                _builder = Strings.GetSpanBasedStringBuilder();
                _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstObject.ToString()));
                _firstObject = null;
            }
            else if (!_firstSpan.IsEmpty)
            {
                _builder = Strings.GetSpanBasedStringBuilder();
#if FEATURE_SPAN
                _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstSpan));
#else
                _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstSpan.ToString()));
#endif
                _firstSpan = new ReadOnlyMemory<char>();
            }
        }
    }
}
