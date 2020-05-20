// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build
{
    #region IInternable
    /// <summary>
    /// Define the methods needed to intern something.
    /// </summary>
    internal interface IInternable
    {
        /// <summary>
        /// The length of the target.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Indexer into the target. Presumed to be fast.
        /// </summary>
        char this[int index] { get; }

        /// <summary>
        /// Convert target to string. Presumed to be slow (and will be called just once).
        /// </summary>
        string ExpensiveConvertToString();

        /// <summary>
        /// Compare target to string. Assumes string is of equal or smaller length than target.
        /// </summary>
        bool StartsWithStringByOrdinalComparison(string other);

        /// <summary>
        /// Reference compare target to string. If target is non-string this should return false.
        /// </summary>
        bool ReferenceEquals(string other);
    }
    #endregion


    #region IInternable Implementations
    /// <summary>
    /// A wrapper over StringBuilder.
    /// </summary>
    internal readonly struct StringBuilderInternTarget : IInternable
    {
        /// <summary>
        /// The held StringBuilder
        /// </summary>
        private readonly StringBuilder _target;

        /// <summary>
        /// Pointless comment about constructor.
        /// </summary>
        internal StringBuilderInternTarget(StringBuilder target)
        {
            _target = target;
        }

        /// <summary>
        /// The length of the target.
        /// </summary>
        public int Length => _target.Length;

        /// <summary>
        /// Indexer into the target. Presumed to be fast.
        /// </summary>
        public char this[int index] => _target[index];

        /// <summary>
        /// Never reference equals to string.
        /// </summary>
        public bool ReferenceEquals(string other) => false;

        /// <summary>
        /// Convert target to string. Presumed to be slow (and will be called just once).
        /// </summary>
        public string ExpensiveConvertToString()
        {
            // PERF NOTE: This will be an allocation hot-spot because the StringBuilder is finally determined to
            // not be internable. There is still only one conversion of StringBuilder into string it has just
            // moved into this single spot.
            return _target.ToString();
        }

        /// <summary>
        /// Compare target to string. Assumes string is of equal or smaller length than target.
        /// </summary>
        public bool StartsWithStringByOrdinalComparison(string other)
        {
#if DEBUG
            ErrorUtilities.VerifyThrow(other.Length <= _target.Length, "should be at most as long as target");
#endif
            int length = other.Length;

            // Backwards because the end of the string is more likely to be different earlier in the loop.
            // For example, C:\project1, C:\project2
            for (int i = length - 1; i >= 0; --i)
            {
                if (_target[i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Don't use this function. Use ExpensiveConvertToString
        /// </summary>
        public override string ToString() => throw new InvalidOperationException();
    }

    /// <summary>
    /// A wrapper over char[].
    /// </summary>
    internal readonly struct CharArrayInternTarget : IInternable
    {
        /// <summary>
        /// Start index for the string
        /// </summary>
        private readonly int _startIndex;

        /// <summary>
        /// The held array
        /// </summary>
        private readonly char[] _target;

        /// <summary>
        /// Pointless comment about constructor.
        /// </summary>
        internal CharArrayInternTarget(char[] target, int count)
            : this(target, 0, count)
        {
        }

        /// <summary>
        /// Pointless comment about constructor.
        /// </summary>
        internal CharArrayInternTarget(char[] target, int startIndex, int count)
        {
#if DEBUG
            if (startIndex + count > target.Length)
            {
                ErrorUtilities.ThrowInternalError("wrong length");
            }
#endif
            _target = target;
            _startIndex = startIndex;
            Length = count;
        }

        /// <summary>
        /// The length of the target.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Indexer into the target. Presumed to be fast.
        /// </summary>
        public char this[int index]
        {
            get
            {
                return _target[index + _startIndex];
            }
        }

        /// <summary>
        /// Convert target to string. Presumed to be slow (and will be called just once).
        /// </summary>
        public bool ReferenceEquals(string other)
        {
            return false;
        }

        /// <summary>
        /// Convert target to string. Presumed to be slow (and will be called just once).
        /// </summary>
        public string ExpensiveConvertToString()
        {
            // PERF NOTE: This will be an allocation hot-spot because the char[] is finally determined to
            // not be internable. There is still only one conversion of char[] into string it has just
            // moved into this single spot.
            return new string(_target, _startIndex, Length);
        }

        /// <summary>
        /// Compare target to string. Assumes string is of equal or smaller length than target.
        /// </summary>
        public bool StartsWithStringByOrdinalComparison(string other)
        {
#if DEBUG
            ErrorUtilities.VerifyThrow(other.Length <= Length, "should be at most as long as target");
#endif
            // Backwards because the end of the string is (by observation of Australian Government build) more likely to be different earlier in the loop.
            // For example, C:\project1, C:\project2
            for (int i = other.Length - 1; i >= 0; --i)
            {
                if (_target[i + _startIndex] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Don't use this function. Use ExpensiveConvertToString
        /// </summary>
        public override string ToString()
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Wrapper over a string.
    /// </summary>
    internal readonly struct StringInternTarget : IInternable
    {
        /// <summary>
        /// Stores the wrapped string.
        /// </summary>
        private readonly string _target;

        /// <summary>
        /// Constructor of the class
        /// </summary>
        /// <param name="target">The string to wrap</param>
        internal StringInternTarget(string target)
        {
            ErrorUtilities.VerifyThrowArgumentLength(target, nameof(target));
            _target = target;
        }

        /// <summary>
        /// Gets the length of the target string.
        /// </summary>
        public int Length => _target.Length;

        /// <summary>
        /// Gets the n character in the target string.
        /// </summary>
        /// <param name="index">Index of the character to gather.</param>
        /// <returns>The character in the position marked by index.</returns>
        public char this[int index] => _target[index];

        /// <summary>
        /// Returns the target which is already a string.
        /// </summary>
        /// <returns>The target string.</returns>
        public string ExpensiveConvertToString() => _target;

        /// <summary>
        /// Compare target to string. Assumes string is of equal or smaller length than target.
        /// </summary>
        /// <param name="other">The string to compare with the target.</param>
        /// <returns>True if target starts with <paramref name="other"/>, false otherwise.</returns>
        public bool StartsWithStringByOrdinalComparison(string other) => _target.StartsWith(other, StringComparison.Ordinal);

        /// <summary>
        /// Verifies if the reference of the target string is the same of the given string.
        /// </summary>
        /// <param name="other">The string reference to compare to.</param>
        /// <returns>True if both references are equal, false otherwise.</returns>
        public bool ReferenceEquals(string other) => ReferenceEquals(_target, other);
    }

    /// <summary>
    /// Wrapper over a substring of a string.
    /// </summary>
    internal readonly struct SubstringInternTarget : IInternable
    {
        /// <summary>
        /// Stores the wrapped string.
        /// </summary>
        private readonly string _target;

        /// <summary>
        /// Start index of the substring within the wrapped string.
        /// </summary>
        private readonly int _startIndex;

        /// <summary>
        /// Constructor of the class
        /// </summary>
        /// <param name="target">The string to wrap.</param>
        /// <param name="startIndex">Start index of the substring within <paramref name="target"/>.</param>
        /// <param name="length">Length of the substring.</param>
        internal SubstringInternTarget(string target, int startIndex, int length)
        {
#if DEBUG
            if (startIndex + length > target.Length)
            {
                ErrorUtilities.ThrowInternalError("wrong length");
            }
#endif
            _target = target;
            _startIndex = startIndex;
            Length = length;
        }

        /// <summary>
        /// Gets the length of the target substring.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the n character in the target substring.
        /// </summary>
        /// <param name="index">Index of the character to gather.</param>
        /// <returns>The character in the position marked by index.</returns>
        public char this[int index] => _target[index + _startIndex];

        /// <summary>
        /// Returns the target substring as a string.
        /// </summary>
        /// <returns>The substring.</returns>
        public string ExpensiveConvertToString() => _target.Substring(_startIndex, Length);

        /// <summary>
        /// Compare target substring to a string. Assumes string is of equal or smaller length than the target substring.
        /// </summary>
        /// <param name="other">The string to compare with the target substring.</param>
        /// <returns>True if target substring starts with <paramref name="other"/>, false otherwise.</returns>
        public bool StartsWithStringByOrdinalComparison(string other) => (String.CompareOrdinal(_target, _startIndex, other, 0, other.Length) == 0);

        /// <summary>
        /// Never reference equals to string.
        /// </summary>
        public bool ReferenceEquals(string other) => false;
    }

    #endregion
}
