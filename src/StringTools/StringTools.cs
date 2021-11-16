// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.NET.StringTools
{
    public static class Strings

    {
        #region Fields

        /// <summary>
        /// Per-thread instance of the SpanBasedStringBuilder, created lazily.
        /// </summary>
        /// <remarks>
        /// This field serves as a per-thread one-item object pool, which is adequate for most use
        /// cases as the builder is not expected to be held for extended periods of time.
        /// </remarks>
        [ThreadStatic]
        private static SpanBasedStringBuilder? _spanBasedStringBuilder;

        #endregion

        #region Public methods

        /// <summary>
        /// Interns the given string, keeping only a weak reference to the returned value.
        /// </summary>
        /// <param name="str">The string to intern.</param>
        /// <returns>A string equal to <paramref name="str"/>, could be the same object as <paramref name="str"/>.</returns>
        /// <remarks>
        /// The intern pool does not retain strong references to the strings it's holding so strings are automatically evicted
        /// after they become unrooted. This is in contrast to <c>System.String.Intern</c> which holds strings forever.
        /// </remarks>
        public static string WeakIntern(string str)
        {
            InternableString internableString = new InternableString(str);
            return WeakStringCacheInterner.Instance.InternableToString(ref internableString);
        }

#if !NET35
        /// <summary>
        /// Interns the given readonly span of characters, keeping only a weak reference to the returned value.
        /// </summary>
        /// <param name="str">The character span to intern.</param>
        /// <returns>A string equal to <paramref name="str"/>, could be the result of calling ToString() on <paramref name="str"/>.</returns>
        /// <remarks>
        /// The intern pool does not retain strong references to the strings it's holding so strings are automatically evicted
        /// after they become unrooted. This is in contrast to <c>System.String.Intern</c> which holds strings forever.
        /// </remarks>
        public static string WeakIntern(ReadOnlySpan<char> str)
        {
            InternableString internableString = new InternableString(str);
            return WeakStringCacheInterner.Instance.InternableToString(ref internableString);
        }
#endif

        /// <summary>
        /// Returns a new or recycled <see cref="SpanBasedStringBuilder"/>.
        /// </summary>
        /// <returns>The SpanBasedStringBuilder.</returns>
        /// <remarks>
        /// Call <see cref="IDisposable.Dispose"/> on the returned instance to recycle it.
        /// </remarks>
        public static SpanBasedStringBuilder GetSpanBasedStringBuilder()
        {
            SpanBasedStringBuilder? stringBuilder = _spanBasedStringBuilder;
            if (stringBuilder == null)
            {
                return new SpanBasedStringBuilder();
            }
            else
            {
                _spanBasedStringBuilder = null;
                return stringBuilder;
            }
        }

        /// <summary>
        /// Enables diagnostics in the interner. Call <see cref="CreateDiagnosticReport"/> to retrieve the diagnostic data.
        /// </summary>
        public static void EnableDiagnostics()
        {
            WeakStringCacheInterner.Instance.EnableStatistics();
        }

        /// <summary>
        /// Retrieves the diagnostic data describing the current state of the interner. Make sure to call <see cref="EnableDiagnostics"/> beforehand.
        /// </summary>
        public static string CreateDiagnosticReport()
        {
            return WeakStringCacheInterner.Instance.FormatStatistics();
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="SpanBasedStringBuilder"/> instance back to the pool if possible.
        /// </summary>
        /// <param name="stringBuilder">The instance to return.</param>
        internal static void ReturnSpanBasedStringBuilder(SpanBasedStringBuilder stringBuilder)
        {
            stringBuilder.Clear();
            _spanBasedStringBuilder = stringBuilder;
        }
    }
}
