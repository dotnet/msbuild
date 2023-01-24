// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xunit.NetCore.Extensions
{
    internal static class Utilities
    {
#if NETFRAMEWORK
        public static bool IsRunningOnNet => false;

        public static bool IsRunningOnNetStandard => false;

        public static bool IsRunningOnNetFramework => true;
#elif NETSTANDARD
        public static bool IsRunningOnNet => false;

        public static bool IsRunningOnNetFramework => false;

        public static bool IsRunningOnNetStandard => true;

#elif NET
        public static bool IsRunningOnNet => true;

        public static bool IsRunningOnNetStandard => false;

        public static bool IsRunningOnNetFramework => false;
#endif

        public static string AppendAdditionalMessage(this string message, string? additionalMessage)
            => !string.IsNullOrWhiteSpace(additionalMessage) ? $"{message} {additionalMessage}" : message;
    }
}
