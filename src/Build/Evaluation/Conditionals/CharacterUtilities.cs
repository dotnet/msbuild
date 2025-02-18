// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

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

        internal static bool IsHexDigit(char candidate)
        {
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_14))
            {
#if NET
                return char.IsAsciiHexDigit(candidate);
#else
                return (candidate - '0' <= '9' - '0') || ((uint)((candidate | 0x20) - 'a') <= 'f' - 'a');
#endif
            }
            else
            {
                return char.IsDigit(candidate) || ((uint)((candidate | 0x20) - 'a') <= 'f' - 'a');
            }
        }
    }
}
