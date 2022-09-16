// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace System
{
    /// <summary>
    /// A bare minimum and inefficient version of MemoryExtensions as provided in System.Memory on .NET 4.5.
    /// </summary>
    public static class MemoryExtensions
    {
        public static string AsSpan<T>(this T[] array, int start, int length)
        {
            if (array is char[] charArray)
            {
                return new string(charArray, start, length);
            }
            throw new ArgumentException(nameof(array));
        }
    }
}

namespace Microsoft.NET.StringTools
{
    /// <summary>
    /// Represents a string that can be converted to System.String with interning, i.e. by returning an existing string if it has been seen before
    /// and is still tracked in the intern table.
    /// </summary>
    /// <remarks>
    /// This is a simple and inefficient implementation compatible with .NET Framework 3.5.
    /// </remarks>
    internal ref struct InternableString
    {
        /// <summary>
        /// Enumerator for the top-level struct. Enumerates characters of the string.
        /// </summary>
        public ref struct Enumerator
        {
            /// <summary>
            /// The InternableString being enumerated.
            /// </summary>
            private InternableString _string;

            /// <summary>
            /// Index of the current character, -1 if MoveNext has not been called yet.
            /// </summary>
            private int _charIndex;

            public Enumerator(ref InternableString spanBuilder)
            {
                _string = spanBuilder;
                _charIndex = -1;
            }

            /// <summary>
            /// Returns the current character.
            /// </summary>
            public char Current => (_string._builder == null ? _string.FirstString[_charIndex] : _string._builder[_charIndex]);

            /// <summary>
            /// Moves to the next character.
            /// </summary>
            /// <returns>True if there is another character, false if the enumerator reached the end.</returns>
            public bool MoveNext()
            {
                int newIndex = _charIndex + 1;
                if (newIndex < _string.Length)
                {
                    _charIndex = newIndex;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// If this instance wraps a StringBuilder, it uses this backing field.
        /// </summary>
        private StringBuilder? _builder;

        /// <summary>
        /// If this instance represents one contiguous string, it may be held in this field.
        /// </summary>
        private string? _firstString;

        /// <summary>
        /// A convenience getter to ensure that we always operate on a non-null string.
        /// </summary>
        private string FirstString => _firstString ?? string.Empty;

        /// <summary>
        /// Constructs a new InternableString wrapping the given string.
        /// </summary>
        /// <param name="str">The string to wrap, must be non-null.</param>
        internal InternableString(string str)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }
            _builder = null;
            _firstString = str;
        }

        /// <summary>
        /// Constructs a new InternableString wrapping the given SpanBasedStringBuilder.
        /// </summary>
        internal InternableString(SpanBasedStringBuilder builder)
        {
            _builder = builder.Builder;
            _firstString = null;
        }

        /// <summary>
        /// Gets the length of the string.
        /// </summary>
        public int Length => (_builder == null ? FirstString.Length : _builder.Length);

        /// <summary>
        /// Creates a new enumerator for enumerating characters in this string. Does not allocate.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        /// <summary>
        /// Returns true if the string is equal to another string by ordinal comparison.
        /// </summary>
        /// <param name="other">Another string.</param>
        /// <returns>True if this string is equal to <paramref name="other"/>.</returns>
        public bool Equals(string other)
        {
            if (other.Length != Length)
            {
                return false;
            }

            if (_firstString != null)
            {
                return _firstString.Equals(other);
            }
            if (_builder != null)
            {
                for (int i = 0; i < other.Length; i++)
                {
                    // Note: This indexing into the StringBuilder could be O(N). We prefer it over allocating
                    // a new string with ToString().
                    if (other[i] != _builder[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Returns a System.String representing this string. Allocates memory unless this InternableString was created by wrapping a
        /// System.String in which case the original string is returned.
        /// </summary>
        /// <returns>The string.</returns>
        public string ExpensiveConvertToString()
        {
            // Special case: if we hold just one string, we can directly return it.
            if (_firstString != null)
            {
                return _firstString;
            }
            return _builder?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Returns true if this InternableString wraps a System.String and the same System.String is passed as the argument.
        /// </summary>
        /// <param name="str">The string to compare to.</param>
        /// <returns>True is this instance wraps the given string.</returns>
        public bool ReferenceEquals(string str)
        {
            return Object.ReferenceEquals(str, _firstString);
        }

        /// <summary>
        /// Converts this instance to a System.String while first searching for a match in the intern table.
        /// </summary>
        /// <remarks>
        /// May allocate depending on whether the string has already been interned.
        /// </remarks>
        public override unsafe string ToString()
        {
            return WeakStringCacheInterner.Instance.InternableToString(ref this);
        }

        /// <summary>
        /// Implements the simple yet very decently performing djb2 hash function (xor version).
        /// </summary>
        /// <returns>A stable hashcode of the string represented by this instance.</returns>
        public override int GetHashCode()
        {
            int hashCode = 5381;

            if (_firstString != null)
            {
                foreach (char ch in _firstString)
                {
                    unchecked
                    {
                        hashCode = hashCode * 33 ^ ch;
                    }
                }
            }
            else if (_builder != null)
            {
                for (int i = 0; i < _builder.Length; i++)
                {
                    unchecked
                    {
                        hashCode = hashCode * 33 ^ _builder[i];
                    }
                }
            }
            return hashCode;
        }
    }
}
