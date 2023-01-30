// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xunit.NetCore.Extensions
{
    public static class CustomXunitAttributesUtilities
    {
#if NETFRAMEWORK
        public static bool IsBuiltAgainstDotNet => false;

        public static bool IsBuiltAgainstNetFramework => true;
#elif NET
        public static bool IsBuiltAgainstDotNet => true;

        public static bool IsBuiltAgainstNetFramework => false;
#endif

        public static string AppendAdditionalMessage(this string message, string? additionalMessage)
            => !string.IsNullOrWhiteSpace(additionalMessage) ? $"{message} {additionalMessage}" : message;
    }
}
