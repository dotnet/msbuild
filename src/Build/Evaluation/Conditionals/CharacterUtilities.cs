// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal static class CharacterUtilities
    {
        internal static bool IsNumberStart(char candidate)
        {
            return candidate == '+' || candidate == '-' || candidate == '.' || IsDigit(candidate);
        }

        internal static bool IsSimpleStringStart(char candidate)
        {
            return candidate == '_' || char.IsLetter(candidate);
        }

        internal static bool IsSimpleStringChar(char candidate)
        {
            return IsSimpleStringStart(candidate) || IsDigit(candidate);
        }

        internal static bool IsDigit(char candidate)
        {
            return candidate >= '0' && candidate <= '9';
        }

        internal static bool IsHexDigit(char candidate)
        {
            return IsDigit(candidate) || ((uint)((candidate | 0x20) - 'a') <= 'f' - 'a');
        }
    }
}
