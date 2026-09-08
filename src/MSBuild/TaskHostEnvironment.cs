// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.CommandLine;

internal static class TaskHostEnvironment
{
    // WOW64 adjusts these variables when creating a process of a different bitness.
    // https://learn.microsoft.com/windows/win32/winprog64/wow64-implementation-details#environment-variables
    private static readonly string[] s_architectureVariables =
    [
        "PROCESSOR_ARCHITECTURE",
        "PROCESSOR_ARCHITEW6432",
        "ProgramFiles",
        "ProgramW6432",
        "CommonProgramFiles",
        "CommonProgramW6432",
    ];

    internal static IDictionary<string, KeyValuePair<string?, string?>> CreateMismatchedEnvironmentTable(
        IDictionary<string, string> taskEnvironment,
        IDictionary<string, string> hostEnvironment,
        bool useLegacyBehavior,
        bool isWindows)
    {
        Dictionary<string, KeyValuePair<string?, string?>> mismatches = new(StringComparer.OrdinalIgnoreCase);

        if (useLegacyBehavior)
        {
            foreach (string name in hostEnvironment.Keys)
            {
                AddMismatch(name);
            }

            foreach (string name in taskEnvironment.Keys)
            {
                if (!hostEnvironment.ContainsKey(name))
                {
                    AddMismatch(name);
                }
            }
        }
        else if (isWindows)
        {
            foreach (string name in s_architectureVariables)
            {
                AddMismatch(name);
            }
        }

        return mismatches;

        void AddMismatch(string name)
        {
            taskEnvironment.TryGetValue(name, out string? taskValue);
            hostEnvironment.TryGetValue(name, out string? hostValue);

            if (!string.Equals(taskValue, hostValue, StringComparison.OrdinalIgnoreCase))
            {
                mismatches[name] = new(taskValue, hostValue);
            }
        }
    }
}
