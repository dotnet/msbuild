// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal static class CharacterUtilities
    {
        internal static bool IsNumberStart(char candidate)
        {
            return candidate == '+' || candidate == '-' || candidate == '.' || char.IsDigit(candidate);
        }

        internal static bool IsSimpleStringStart(char candidate)
        {
            return candidate == '_' || char.IsLetter(candidate);
        }

        internal static bool IsSimpleStringChar(char candidate)
        {
            return IsSimpleStringStart(candidate) || char.IsDigit(candidate);
        }

        internal static bool IsHexAlphabetic(char candidate)
        {
            return candidate == 'a' || candidate == 'b' || candidate == 'c' || candidate == 'd' || candidate == 'e' || candidate == 'f' ||
                candidate == 'A' || candidate == 'B' || candidate == 'C' || candidate == 'D' || candidate == 'E' || candidate == 'F';
        }

        internal static bool IsHexDigit(char candidate)
        {
            return char.IsDigit(candidate) || IsHexAlphabetic(candidate);
        }
    }
}
