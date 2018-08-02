// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Shared.EscapingStringExtensions
{
    internal static class EscapingStringExtensions
    {
        internal static string Unescape(this string escapedString)
        {
            return EscapingUtilities.UnescapeAll(escapedString);
        }

        internal static string Unescape
        (
            this string escapedString,
            out bool escapingWasNecessary
        )
        {
            return EscapingUtilities.UnescapeAll(escapedString, out escapingWasNecessary);
        }

        internal static string Escape(this string unescapedString)
        {
            return EscapingUtilities.Escape(unescapedString);
        }

        internal static bool ContainsEscapedWildcards(this string escapedString)
        {
            return EscapingUtilities.ContainsEscapedWildcards(escapedString);
        }
    }
}