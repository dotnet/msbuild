// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

namespace Microsoft.Build.BuildEngine
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
