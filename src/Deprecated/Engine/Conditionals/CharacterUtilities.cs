// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace Microsoft.Build.BuildEngine
{
    internal static class CharacterUtilities
    {
        internal static bool IsNumberStart(char candidate)
        {
            return (candidate == '+' || candidate == '-' || candidate == '.' || char.IsDigit(candidate));
        }

        internal static bool IsSimpleStringStart(char candidate)
        {
            return (candidate == '_' || char.IsLetter(candidate));
        }

        internal static bool IsSimpleStringChar(char candidate)
        {
            return (IsSimpleStringStart(candidate) || char.IsDigit(candidate));
        }

        internal static bool IsHexAlphabetic(char candidate)
        {
            return (candidate == 'a' || candidate == 'b' || candidate == 'c' || candidate == 'd' || candidate == 'e' || candidate == 'f' ||
                candidate == 'A' || candidate == 'B' || candidate == 'C' || candidate == 'D' || candidate == 'E' || candidate == 'F');
        }

        internal static bool IsHexDigit(char candidate)
        {
            return (char.IsDigit(candidate) || IsHexAlphabetic(candidate));
        }
    }
}
