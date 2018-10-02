// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace Microsoft.Build.Evaluation
{
    internal static class CharacterUtilities
    {
        static internal bool IsNumberStart(char candidate)
        {
            return (candidate == '+' || candidate == '-' || candidate == '.' || char.IsDigit(candidate));
        }

        static internal bool IsSimpleStringStart(char candidate)
        {
            return (candidate == '_' || char.IsLetter(candidate));
        }

        static internal bool IsSimpleStringChar(char candidate)
        {
            return (IsSimpleStringStart(candidate) || char.IsDigit(candidate));
        }

        static internal bool IsHexAlphabetic(char candidate)
        {
            return (candidate == 'a' || candidate == 'b' || candidate == 'c' || candidate == 'd' || candidate == 'e' || candidate == 'f' ||
                candidate == 'A' || candidate == 'B' || candidate == 'C' || candidate == 'D' || candidate == 'E' || candidate == 'F');
        }

        static internal bool IsHexDigit(char candidate)
        {
            return (char.IsDigit(candidate) || IsHexAlphabetic(candidate));
        }
    }
}
