// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.NET.StringTools
{
    /// <summary>
    /// A StringBuilder replacement that keeps a list of <see cref="ReadOnlyMemory{T}"/> spans making up the intermediate string rather
    /// than a copy of its characters. This has positive impact on both memory (no need to allocate space for the intermediate string)
    /// and time (no need to copy characters to the intermediate string).
    /// </summary>
    /// <remarks>
    /// The <see cref="ToString"/> method tries to intern the resulting string without even allocating it if it's already interned.
    /// Use <see cref="Strings.GetSpanBasedStringBuilder"/> to take advantage of pooling to eliminate allocation overhead of this class.
    /// </remarks>
    public class SpanBasedStringBuilder : IDisposable
    {
        /// <summary>
        /// Enumerator for the top-level class. Enumerates characters of the string.
        /// </summary>
        public struct Enumerator
        {
            /// <summary>
            /// The spans being enumerated.
            /// </summary>
            private readonly List<ReadOnlyMemory<char>> _spans;

            /// <summary>
            /// Index of the current span.
            /// </summary>
            private int _spanIndex;

            /// <summary>
            /// Index of the current character in the current span, -1 if MoveNext has not been called yet.
            /// </summary>
            private int _charIndex;

            internal Enumerator(List<ReadOnlyMemory<char>> spans)
            {
                _spans = spans;
                _spanIndex = 0;
                _charIndex = -1;
            }

            /// <summary>
            /// Returns the current character.
            /// </summary>
            public readonly char Current
            {
                get
                {
                    ReadOnlyMemory<char> span = _spans[_spanIndex];
                    return span.Span[_charIndex];
                }
            }

            /// <summary>
            /// Moves to the next character.
            /// </summary>
            /// <returns>True if there is another character, false if the enumerator reached the end.</returns>
            public bool MoveNext()
            {
                int newCharIndex = _charIndex + 1;
                while (_spanIndex < _spans.Count)
                {
                    if (newCharIndex < _spans[_spanIndex].Length)
                    {
                        _charIndex = newCharIndex;
                        return true;
                    }
                    _spanIndex++;
                    newCharIndex = 0;
                }
                return false;
            }
        }

        /// <summary>
        /// Spans making up the rope.
        /// </summary>
        private readonly List<ReadOnlyMemory<char>> _spans;

        /// <summary>
        /// Internal getter to get the list of spans out of the SpanBasedStringBuilder.
        /// </summary>
        internal List<ReadOnlyMemory<char>> Spans => _spans;

        /// <summary>
        /// Constructs a new SpanBasedStringBuilder containing the given string.
        /// </summary>
        /// <param name="str">The string to wrap, must be non-null.</param>
        public SpanBasedStringBuilder(string str)
            : this()
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }
            Append(str);
        }

        /// <summary>
        /// Constructs a new empty SpanBasedStringBuilder with the given expected number of spans.
        /// </summary>
        public SpanBasedStringBuilder(int capacity = 4)
        {
            _spans = new List<ReadOnlyMemory<char>>(capacity);
            Length = 0;
        }

        /// <summary>
        /// Gets the length of the string.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets the capacity of the SpanBasedStringBuilder in terms of number of spans it can hold without allocating.
        /// </summary>
        public int Capacity => _spans.Capacity;

        /// <summary>
        /// Creates a new enumerator for enumerating characters in this string. Does not allocate.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_spans);
        }

        /// <summary>
        /// Converts this instance to a System.String while first searching for a match in the intern table.
        /// </summary>
        /// <remarks>
        /// May allocate depending on whether the string has already been interned.
        /// </remarks>
        public override string ToString()
        {
            return new InternableString(this).ToString();
        }

        /// <summary>
        /// Releases this instance.
        /// </summary>
        public void Dispose()
        {
            Strings.ReturnSpanBasedStringBuilder(this);
        }

        #region Public mutating methods

        /// <summary>
        /// Appends a string.
        /// </summary>
        /// <param name="value">The string to append.</param>
        public void Append(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _spans.Add(value.AsMemory());
                Length += value.Length;
            }
        }

        /// <summary>
        /// Appends a substring.
        /// </summary>
        /// <param name="value">The string to append.</param>
        /// <param name="startIndex">The start index of the substring within <paramref name="value"/> to append.</param>
        /// <param name="count">The length of the substring to append.</param>
        public void Append(string value, int startIndex, int count)
        {
            if (value != null)
            {
                if (count > 0)
                {
                    _spans.Add(value.AsMemory(startIndex, count));
                    Length += count;
                }
            }
            else
            {
                if (startIndex != 0 || count != 0)
                {
                    throw new ArgumentNullException(nameof(value));
                }
            }
        }

        /// <summary>
        /// Appends a character span represented by <see cref="ReadOnlyMemory{T}" />.
        /// </summary>
        /// <param name="span">The character span to append.</param>
        public void Append(ReadOnlyMemory<char> span)
        {
            if (!span.IsEmpty)
            {
                _spans.Add(span);
                Length += span.Length;
            }
        }

        /// <summary>
        /// Removes leading white-space characters from the string.
        /// </summary>
        public void TrimStart()
        {
            for (int spanIdx = 0; spanIdx < _spans.Count; spanIdx++)
            {
                ReadOnlySpan<char> span = _spans[spanIdx].Span;
                int i = 0;
                while (i < span.Length && char.IsWhiteSpace(span[i]))
                {
                    i++;
                }
                if (i > 0)
                {
                    _spans[spanIdx] = _spans[spanIdx].Slice(i);
                    Length -= i;
                }
                if (!_spans[spanIdx].IsEmpty)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Removes trailing white-space characters from the string.
        /// </summary>
        public void TrimEnd()
        {
            for (int spanIdx = _spans.Count - 1; spanIdx >= 0; spanIdx--)
            {
                ReadOnlySpan<char> span = _spans[spanIdx].Span;
                int i = span.Length - 1;
                while (i >= 0 && char.IsWhiteSpace(span[i]))
                {
                    i--;
                }
                if (i + 1 < span.Length)
                {
                    _spans[spanIdx] = _spans[spanIdx].Slice(0, i + 1);
                    Length -= span.Length - (i + 1);
                }
                if (!_spans[spanIdx].IsEmpty)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Removes leading and trailing white-space characters from the string.
        /// </summary>
        public void Trim()
        {
            TrimStart();
            TrimEnd();
        }

        /// <summary>
        /// Clears this instance making it represent an empty string.
        /// </summary>
        public void Clear()
        {
            _spans.Clear();
            Length = 0;
        }

        #endregion
    }
}
