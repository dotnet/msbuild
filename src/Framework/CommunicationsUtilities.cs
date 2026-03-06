// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if !CLR2COMPATIBILITY
using System.Collections.Frozen;
#endif

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Utilities for environment variable operations in the Framework project.
    /// This class copies some of the methods from the Shared/CommunicationsUtilities. 
    /// IMPORTANT NOTE: Some of the functions were simplified to remove the dependencies from Shared files. 
    /// Do not use the versions from this file unless calling from Framework project and you have verified that these functions meet your needs.
    /// </summary>
    internal static class FrameworkCommunicationsUtilities
    {
#if !CLR2COMPATIBILITY
        /// <summary>
        /// A set of environment variables cached from the last time we called GetEnvironmentVariables.
        /// Used to avoid allocations if the environment has not changed.
        /// </summary>
        private static EnvironmentState s_environmentState;
#endif

        /// <summary>
        /// On Windows, environment variables should be case-insensitive;
        /// on Unix-like systems, they should be case-sensitive, but this might be a breaking change in an edge case.
        /// https://github.com/dotnet/msbuild/issues/12858
        /// </summary>
        internal static StringComparer EnvironmentVariableComparer => StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Get environment block.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        internal static extern unsafe char* GetEnvironmentStrings();

        /// <summary>
        /// Free environment block.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        internal static extern unsafe bool FreeEnvironmentStrings(char* pStrings);

#if NETFRAMEWORK
        /// <summary>
        /// Set environment variable P/Invoke.
        /// </summary>
        [DllImport("kernel32.dll", EntryPoint = "SetEnvironmentVariable", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEnvironmentVariableNative(string name, string value);

        /// <summary>
        /// Sets an environment variable using P/Invoke to workaround the .NET Framework BCL implementation.
        /// </summary>
        /// <remarks>
        /// .NET Framework implementation of SetEnvironmentVariable checks the length of the value and throws an exception if
        /// it's greater than or equal to 32,767 characters. This limitation does not exist on modern Windows or .NET.
        /// </remarks>
        internal static void SetEnvironmentVariable(string name, string value)
        {
            if (!SetEnvironmentVariableNative(name, value))
            {
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }
#endif

#if !CLR2COMPATIBILITY
        /// <summary>
        /// A container to atomically swap a cached set of environment variables and the block string used to create it.
        /// The environment block property will only be set on Windows, since on Unix we need to directly call
        /// Environment.GetEnvironmentVariables().
        /// </summary>
        private sealed record class EnvironmentState(FrozenDictionary<string, string> EnvironmentVariables, ReadOnlyMemory<char> EnvironmentBlock = default);
#endif

        /// <summary>
        /// Returns key value pairs of environment variables in a new dictionary
        /// with a case-insensitive key comparer.
        /// IMPORTANT NOTE: this function was simplified to remove the dependencies from Shared files.
        /// </summary>
        /// <remarks>
        /// Copied from the BCL implementation to eliminate some expensive security asserts on .NET Framework.
        /// </remarks>
#if CLR2COMPATIBILITY
        internal static Dictionary<string, string> GetEnvironmentVariables()
        {
#else
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static FrozenDictionary<string, string> GetEnvironmentVariablesWindows()
        {
#endif
            unsafe
            {
                char* pEnvironmentBlock = null;

                try
                {
                    pEnvironmentBlock = GetEnvironmentStrings();
                    if (pEnvironmentBlock == null)
                    {
                        throw new OutOfMemoryException();
                    }

                    // Search for terminating \0\0 (two unicode \0's).
                    char* pEnvironmentBlockEnd = pEnvironmentBlock;
                    while (!(*pEnvironmentBlockEnd == '\0' && *(pEnvironmentBlockEnd + 1) == '\0'))
                    {
                        pEnvironmentBlockEnd++;
                    }
                    long stringBlockLength = pEnvironmentBlockEnd - pEnvironmentBlock;

#if !CLR2COMPATIBILITY
                    // Avoid allocating any objects if the environment still matches the last state.
                    // We speed this up by comparing the full block instead of individual key-value pairs.
                    ReadOnlySpan<char> stringBlock = new(pEnvironmentBlock, (int)stringBlockLength);
                    EnvironmentState lastState = s_environmentState;
                    if (lastState?.EnvironmentBlock.Span.SequenceEqual(stringBlock) == true)
                    {
                        return lastState.EnvironmentVariables;
                    }
#endif

                    Dictionary<string, string> table = new(200, StringComparer.OrdinalIgnoreCase); // Razzle has 150 environment variables

                    // Copy strings out, parsing into pairs and inserting into the table.
                    // The first few environment variable entries start with an '='!
                    // The current working directory of every drive (except for those drives
                    // you haven't cd'ed into in your DOS window) are stored in the
                    // environment block (as =C:=pwd) and the program's exit code is
                    // as well (=ExitCode=00000000)  Skip all that start with =.
                    // Read docs about Environment Blocks on MSDN's CreateProcess page.

                    // Format for GetEnvironmentStrings is:
                    // (=HiddenVar=value\0 | Variable=value\0)* \0
                    // See the description of Environment Blocks in MSDN's
                    // CreateProcess page (null-terminated array of null-terminated strings).
                    // Note the =HiddenVar's aren't always at the beginning.
                    for (int i = 0; i < stringBlockLength; i++)
                    {
                        int startKey = i;

                        // Skip to key
                        // On some old OS, the environment block can be corrupted.
                        // Some lines will not have '=', so we need to check for '\0'.
                        while (*(pEnvironmentBlock + i) != '=' && *(pEnvironmentBlock + i) != '\0')
                        {
                            i++;
                        }

                        if (*(pEnvironmentBlock + i) == '\0')
                        {
                            continue;
                        }

                        // Skip over environment variables starting with '='
                        if (i - startKey == 0)
                        {
                            while (*(pEnvironmentBlock + i) != 0)
                            {
                                i++;
                            }

                            continue;
                        }

                        string key = new string(pEnvironmentBlock, startKey, i - startKey);

                        i++;

                        // skip over '='
                        int startValue = i;

                        while (*(pEnvironmentBlock + i) != 0)
                        {
                            // Read to end of this entry
                            i++;
                        }


                        string value = new string(pEnvironmentBlock, startValue, i - startValue);

                        // skip over 0 handled by for loop's i++
                        table[key] = value;
                    }

#if !CLR2COMPATIBILITY
                    // Update with the current state.
                    EnvironmentState currentState =
                        new(table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), stringBlock.ToArray());
                    s_environmentState = currentState;
                    return currentState.EnvironmentVariables;
#else
                    return table;
#endif
                }
                finally
                {
                    if (pEnvironmentBlock != null)
                    {
                        FreeEnvironmentStrings(pEnvironmentBlock);
                    }
                }
            }
        }

#if NET
        /// <summary>
        /// Sets an environment variable using <see cref="Environment.SetEnvironmentVariable(string,string)" />.
        /// </summary>
        internal static void SetEnvironmentVariable(string name, string value)
            => Environment.SetEnvironmentVariable(name, value);
#endif

#if !CLR2COMPATIBILITY
        /// <summary>
        /// Returns key value pairs of environment variables in a read-only dictionary
        /// with a case-insensitive key comparer.
        ///
        /// If the environment variables have not changed since the last time
        /// this method was called, the same dictionary instance will be returned.
        /// </summary>
        internal static FrozenDictionary<string, string> GetEnvironmentVariables()
        {
            // Always call the native method on Windows, as we'll be able to avoid the internal
            // string and Hashtable allocations caused by Environment.GetEnvironmentVariables().
            if (NativeMethods.IsWindows)
            {
                return GetEnvironmentVariablesWindows();
            }

            IDictionary vars = Environment.GetEnvironmentVariables();

            // Directly use the enumerator since Current will box DictionaryEntry.
            IDictionaryEnumerator enumerator = vars.GetEnumerator();

            // If every key-value pair matches the last state, return a cached dictionary.
            FrozenDictionary<string, string> lastEnvironmentVariables = s_environmentState?.EnvironmentVariables;
            if (vars.Count == lastEnvironmentVariables?.Count)
            {
                bool sameState = true;

                while (enumerator.MoveNext() && sameState)
                {
                    DictionaryEntry entry = enumerator.Entry;
                    if (!lastEnvironmentVariables.TryGetValue((string)entry.Key, out string value)
                        || !string.Equals((string)entry.Value, value, StringComparison.Ordinal))
                    {
                        sameState = false;
                    }
                }

                if (sameState)
                {
                    return lastEnvironmentVariables;
                }
            }

            // Otherwise, allocate and update with the current state.
            Dictionary<string, string> table = new(vars.Count, EnvironmentVariableComparer);

            enumerator.Reset();
            while (enumerator.MoveNext())
            {
                DictionaryEntry entry = enumerator.Entry;
                table[(string)entry.Key] = (string)entry.Value;
            }

            EnvironmentState newState = new(table.ToFrozenDictionary(EnvironmentVariableComparer));
            s_environmentState = newState;

            return newState.EnvironmentVariables;
        }
#endif

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// </summary>
        internal static void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            if (newEnvironment != null)
            {
                // First, delete all no longer set variables
                IDictionary<string, string> currentEnvironment = GetEnvironmentVariables();
                foreach (KeyValuePair<string, string> entry in currentEnvironment)
                {
                    if (!newEnvironment.ContainsKey(entry.Key))
                    {
                        SetEnvironmentVariable(entry.Key, null);
                    }
                }

                // Then, make sure the new ones have their new values.
                foreach (KeyValuePair<string, string> entry in newEnvironment)
                {
                    if (!currentEnvironment.TryGetValue(entry.Key, out string currentValue) || currentValue != entry.Value)
                    {
                        SetEnvironmentVariable(entry.Key, entry.Value);
                    }
                }
            }
        }
    }
}
