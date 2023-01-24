// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.UnitTests
{
    internal static class CustomXunitAttributesUtilities
    {
#if NETFRAMEWORK
        internal static bool IsBuiltAgainstDotNet => false;

        internal static bool IsBuiltAgainstNetFramework => true;
#elif NET
        internal static bool IsBuiltAgainstDotNet => true;

        internal static bool IsBuiltAgainstNetFramework => false;
#endif

        internal static string AppendAdditionalMessage(this string message, string? additionalMessage)
            => !string.IsNullOrWhiteSpace(additionalMessage) ? $"{message} {additionalMessage}" : message;
    }
}
