// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Framework;

internal static partial class EnvironmentUtilities
{
#if !NET
    private static volatile int s_processId;
    private static volatile string? s_processPath;
#endif

    private static volatile string? s_processName;

    /// <summary>
    ///  Gets the unique identifier for the current process.
    /// </summary>
    /// <value>
    ///  A number that represents the unique identifier for the current process.
    /// </value>
    public static int CurrentProcessId
    {
        get
        {
#if NET
            return Environment.ProcessId;
#else
            // copied from Environment.ProcessPath
            int processId = s_processId;
            if (processId == 0)
            {
                using Process currentProcess = Process.GetCurrentProcess();
                s_processId = processId = currentProcess.Id;

                // Assume that process Id zero is invalid for user processes. It holds for all mainstream operating systems.
                Debug.Assert(processId != 0);
            }

            return processId;
#endif
        }
    }

    /// <summary>
    ///  Returns the path of the executable that started the currently executing process.
    ///  Returns <see langword="null"/> when the path is not available.
    /// </summary>
    /// <value>
    ///  The path of the executable that started the currently executing process.
    /// </value>
    /// <remarks>
    ///  If the executable is renamed or deleted before this property is first accessed,
    ///  the return value is undefined and depends on the operating system.
    /// </remarks>
    public static string? ProcessPath
    {
        get
        {
#if NET
            return Environment.ProcessPath;
#else
            // copied from Environment.ProcessPath
            string? processPath = s_processPath;
            if (processPath == null)
            {
                // The value is cached both as a performance optimization and to ensure that the API always returns
                // the same path in a given process.
                using Process currentProcess = Process.GetCurrentProcess();
                Interlocked.CompareExchange(ref s_processPath, currentProcess?.MainModule?.FileName ?? "", null);
                processPath = s_processPath;
                Debug.Assert(processPath != null);
            }

            return (processPath?.Length != 0) ? processPath : null;
#endif
        }
    }

    public static string ProcessName
    {
        get
        {
            string? processName = s_processName;
            if (processName == null)
            {
                using Process currentProcess = Process.GetCurrentProcess();
                Interlocked.CompareExchange(ref s_processName, currentProcess.ProcessName, null);
                processName = s_processName;
            }

            return processName;
        }
    }

    public static bool IsWellKnownEnvironmentDerivedProperty(string propertyName)
        => propertyName.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
           propertyName.StartsWith("COMPLUS_", StringComparison.OrdinalIgnoreCase) ||
           propertyName.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Gets the value of the specified environment variable as an <see cref="int"/>, 
    ///  or returns <paramref name="defaultValue"/> if the variable is not set or cannot be parsed.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <param name="defaultValue">The value to return if the variable is not set or is not a valid integer.</param>
    /// <returns>
    ///  The parsed integer value of the environment variable, or <paramref name="defaultValue"/>
    ///  if the variable is not set, is empty, or cannot be parsed as an integer.
    /// </returns>
    public static int GetValueAsInt32OrDefault(string name, int defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        return !string.IsNullOrEmpty(value) && int.TryParseInvariant(value, out int result)
            ? result
            : defaultValue;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the specified environment variable exists and has a non-empty value;
    ///  otherwise, <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <param name="defaultValue">The value to return if the variable is not set or is empty.</param>
    /// <returns>
    ///  <see langword="true"/> if the environment variable has a non-empty value;
    ///  otherwise, <paramref name="defaultValue"/>.
    /// </returns>
    public static bool ValueExistsOrDefault(string name, bool defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        return !string.IsNullOrEmpty(value) || defaultValue;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the specified environment variable is set to <c>"1"</c>
    ///  or <c>"true"</c> (case-insensitive).
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns>
    ///  <see langword="true"/> if the environment variable value is <c>"1"</c> or <c>"true"</c>
    ///  (case-insensitive); otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsValueOneOrTrue(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        return value != null &&
              (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
    }
}
