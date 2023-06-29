// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NETFRAMEWORK
using System.IO;
#endif
using System.Text;

#nullable disable

namespace Microsoft.Build.Shared
{
    internal static class StringExtensions
    {
        public static string Replace(this string aString, string oldValue, string newValue, StringComparison stringComparison)
        {
            ErrorUtilities.VerifyThrowArgumentNull(aString, nameof(aString));
            ErrorUtilities.VerifyThrowArgumentNull(oldValue, nameof(oldValue));
            ErrorUtilities.VerifyThrowArgumentLength(oldValue, nameof(oldValue));

            if (newValue == null)
            {
                newValue = string.Empty;
            }

            var currentOccurrence = aString.IndexOf(oldValue, stringComparison);

            if (currentOccurrence == -1)
            {
                return aString;
            }

            var endOfPreviousOccurrence = 0;

            // Assumes one match. Optimizes for replacing fallback property values (e.g. MSBuildExtensionsPath), where an import usually references the fallback property once.
            // Reduces memory usage by half.
            var builder = new StringBuilder(aString.Length - oldValue.Length + newValue.Length);

            while (currentOccurrence != -1)
            {
                var nonMatchLength = currentOccurrence - endOfPreviousOccurrence;
                builder.Append(aString, endOfPreviousOccurrence, nonMatchLength);
                builder.Append(newValue);

                endOfPreviousOccurrence = currentOccurrence + oldValue.Length;
                currentOccurrence = aString.IndexOf(oldValue, endOfPreviousOccurrence, stringComparison);
            }

            builder.Append(aString, endOfPreviousOccurrence, aString.Length - endOfPreviousOccurrence);

            return builder.ToString();
        }

#if NETFRAMEWORK
        /// <summary>
        /// Trivial implementation of CommonPrefixLength on spans of characters.
        /// </summary>
        public static int CommonPrefixLength(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
        {
            int commonPrefixLength = 0;
            int length = Math.Min(span.Length, other.Length);

            while (commonPrefixLength < length && span[commonPrefixLength] == other[commonPrefixLength])
            {
                commonPrefixLength++;
            }
            return commonPrefixLength;
        }

        /// <summary>
        /// Adds the missing span-taking overload to .NET Framework version of <see cref="StringBuilder"/>.
        /// </summary>
        public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
        {
            return sb.Append(value.ToString());
        }

        /// <summary>
        /// Adds the missing span-taking overload to .NET Framework version of <see cref="TextWriter"/>.
        /// </summary>
        public static void Write(this TextWriter writer, ReadOnlySpan<char> buffer)
        {
            writer.Write(buffer.ToString());
        }

        /// <summary>
        /// Adds the missing span-taking overload to .NET Framework version of <see cref="TextWriter"/>.
        /// </summary>
        public static void WriteLine(this TextWriter writer, ReadOnlySpan<char> buffer)
        {
            writer.WriteLine(buffer.ToString());
        }
#endif
    }
}
