// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace Microsoft.NET.StringTools
{
    /// <summary>
    /// A simple version of SpanBasedStringBuilder to be used on .NET Framework 3.5. Wraps a <see cref="StringBuilder"/>.
    /// </summary>
    public class SpanBasedStringBuilder : IDisposable
    {
        /// <summary>
        /// Enumerator for the top-level struct. Enumerates characters of the string.
        /// </summary>
        public struct Enumerator
        {
            /// <summary>
            /// The StringBuilder being enumerated.
            /// </summary>
            private StringBuilder _builder;

            /// <summary>
            /// Index of the current character, -1 if MoveNext has not been called yet.
            /// </summary>
            private int _charIndex;

            public Enumerator(StringBuilder builder)
            {
                _builder = builder;
                _charIndex = -1;
            }

            /// <summary>
            /// Returns the current character.
            /// </summary>
            public char Current => _builder[_charIndex];

            /// <summary>
            /// Moves to the next character.
            /// </summary>
            /// <returns>True if there is another character, false if the enumerator reached the end.</returns>
            public bool MoveNext()
            {
                int newIndex = _charIndex + 1;
                if (newIndex < _builder.Length)
                {
                    _charIndex = newIndex;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The backing StringBuilder.
        /// </summary>
        private StringBuilder _builder;

        internal StringBuilder Builder => _builder;

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
            // Since we're using StringBuilder as the backing store in this implementation, our capacity is expressed
            // in number of characters rather than number of spans. We use 128 as a reasonable expected multiplier to
            // go from one to the other, i.e. by default we'll preallocate a 512-character StringBuilder.
            _builder = new StringBuilder(capacity * 128);
        }

        /// <summary>
        /// Gets the length of the string.
        /// </summary>
        public int Length => _builder.Length;

        /// <summary>
        /// Creates a new enumerator for enumerating characters in this string. Does not allocate.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_builder);
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
        internal void Append(string value)
        {
            _builder.Append(value);
        }

        /// <summary>
        /// Appends a substring.
        /// </summary>
        /// <param name="value">The string to append.</param>
        /// <param name="startIndex">The start index of the substring within <paramref name="value"/> to append.</param>
        /// <param name="count">The length of the substring to append.</param>
        internal void Append(string value, int startIndex, int count)
        {
            _builder.Append(value, startIndex, count);
        }

        /// <summary>
        /// Clears this instance making it represent an empty string.
        /// </summary>
        public void Clear()
        {
            _builder.Length = 0;
        }

        #endregion
    }
}
