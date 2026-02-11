// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Defines the banned API entries with their symbol documentation IDs, category, and diagnostic message.
    /// File path APIs (MSBuildTask0003) are handled separately via pattern matching, not symbol matching.
    /// </summary>
    internal static class BannedApiDefinitions
    {
        internal enum ApiCategory
        {
            /// <summary>MSBuildTask0001: Critical errors - no safe alternative.</summary>
            CriticalError,
            /// <summary>MSBuildTask0002: Requires TaskEnvironment replacement.</summary>
            TaskEnvironment,
            /// <summary>MSBuildTask0004: Potential issue - review required.</summary>
            PotentialIssue,
        }

        internal readonly struct BannedApi
        {
            public string DeclarationId { get; }
            public ApiCategory Category { get; }
            public string Message { get; }

            public BannedApi(string declarationId, ApiCategory category, string message)
            {
                DeclarationId = declarationId;
                Category = category;
                Message = message;
            }
        }

        private static readonly ImmutableArray<BannedApi> s_all = CreateAll();

        public static ImmutableArray<BannedApi> GetAll() => s_all;

        private static ImmutableArray<BannedApi> CreateAll()
        {
            return ImmutableArray.Create(
                // ══════════════════════════════════════════════════════════════
                // MSBuildTask0001: Critical errors - no safe alternative
                // Console.* is handled at the TYPE level in the analyzer.
                // ══════════════════════════════════════════════════════════════

                // Process termination
                new BannedApi("M:System.Environment.Exit(System.Int32)",
                    ApiCategory.CriticalError, "terminates entire process; return false or throw instead"),
                new BannedApi("M:System.Environment.FailFast(System.String)",
                    ApiCategory.CriticalError, "terminates entire process; return false or throw instead"),
                new BannedApi("M:System.Environment.FailFast(System.String,System.Exception)",
                    ApiCategory.CriticalError, "terminates entire process; return false or throw instead"),
                new BannedApi("M:System.Environment.FailFast(System.String,System.Exception,System.String)",
                    ApiCategory.CriticalError, "terminates entire process; return false or throw instead"),

                // Process kill
                new BannedApi("M:System.Diagnostics.Process.Kill",
                    ApiCategory.CriticalError, "may terminate the MSBuild host process"),
                new BannedApi("M:System.Diagnostics.Process.Kill(System.Boolean)",
                    ApiCategory.CriticalError, "may terminate the MSBuild host process"),

                // ThreadPool - process-wide settings
                new BannedApi("M:System.Threading.ThreadPool.SetMinThreads(System.Int32,System.Int32)",
                    ApiCategory.CriticalError, "modifies process-wide thread pool settings"),
                new BannedApi("M:System.Threading.ThreadPool.SetMaxThreads(System.Int32,System.Int32)",
                    ApiCategory.CriticalError, "modifies process-wide thread pool settings"),

                // CultureInfo defaults - affect all new threads
                new BannedApi("P:System.Globalization.CultureInfo.DefaultThreadCurrentCulture",
                    ApiCategory.CriticalError, "affects culture of all new threads in process"),
                new BannedApi("P:System.Globalization.CultureInfo.DefaultThreadCurrentUICulture",
                    ApiCategory.CriticalError, "affects UI culture of all new threads in process"),

                // Directory.SetCurrentDirectory - affects all threads
                new BannedApi("M:System.IO.Directory.SetCurrentDirectory(System.String)",
                    ApiCategory.CriticalError, "modifies process-wide working directory; use TaskEnvironment.ProjectDirectory instead"),

                // ══════════════════════════════════════════════════════════════
                // MSBuildTask0002: TaskEnvironment required
                // ══════════════════════════════════════════════════════════════

                // Environment.CurrentDirectory
                new BannedApi("P:System.Environment.CurrentDirectory",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.ProjectDirectory instead"),

                // Directory.GetCurrentDirectory
                new BannedApi("M:System.IO.Directory.GetCurrentDirectory",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.ProjectDirectory instead"),

                // Environment variable access
                new BannedApi("M:System.Environment.GetEnvironmentVariable(System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetEnvironmentVariable instead"),
                new BannedApi("M:System.Environment.GetEnvironmentVariable(System.String,System.EnvironmentVariableTarget)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetEnvironmentVariable instead"),
                new BannedApi("M:System.Environment.GetEnvironmentVariables",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetEnvironmentVariables instead"),
                new BannedApi("M:System.Environment.SetEnvironmentVariable(System.String,System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.SetEnvironmentVariable instead"),
                new BannedApi("M:System.Environment.SetEnvironmentVariable(System.String,System.String,System.EnvironmentVariableTarget)",
                    ApiCategory.TaskEnvironment, "drop target parameter; use TaskEnvironment.SetEnvironmentVariable instead"),
                new BannedApi("M:System.Environment.ExpandEnvironmentVariables(System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetEnvironmentVariable for individual variables instead"),

                // Environment.GetFolderPath - uses process-wide state
                new BannedApi("M:System.Environment.GetFolderPath(System.Environment.SpecialFolder)",
                    ApiCategory.TaskEnvironment, "may be affected by environment variable overrides; use TaskEnvironment.GetEnvironmentVariable instead"),
                new BannedApi("M:System.Environment.GetFolderPath(System.Environment.SpecialFolder,System.Environment.SpecialFolderOption)",
                    ApiCategory.TaskEnvironment, "may be affected by environment variable overrides; use TaskEnvironment.GetEnvironmentVariable instead"),

                // Path.GetFullPath
                new BannedApi("M:System.IO.Path.GetFullPath(System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetAbsolutePath instead"),
                new BannedApi("M:System.IO.Path.GetFullPath(System.String,System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetAbsolutePath instead"),

                // Path.GetTempPath / GetTempFileName - depend on environment variables
                new BannedApi("M:System.IO.Path.GetTempPath",
                    ApiCategory.TaskEnvironment, "depends on TMP/TEMP environment variables; use TaskEnvironment.GetEnvironmentVariable(\"TMP\") instead"),
                new BannedApi("M:System.IO.Path.GetTempFileName",
                    ApiCategory.TaskEnvironment, "depends on TMP/TEMP environment variables; use TaskEnvironment.GetEnvironmentVariable(\"TMP\") instead"),

                // Process.Start - use TaskEnvironment.GetProcessStartInfo
                new BannedApi("M:System.Diagnostics.Process.Start(System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo instead"),
                new BannedApi("M:System.Diagnostics.Process.Start(System.String,System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo instead"),
                new BannedApi("M:System.Diagnostics.Process.Start(System.String,System.Collections.Generic.IEnumerable{System.String})",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo instead"),
                new BannedApi("M:System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo instead"),
                new BannedApi("M:System.Diagnostics.Process.Start(System.String,System.String,System.String,System.Security.SecureString,System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo instead"),

                // ProcessStartInfo constructors
                new BannedApi("M:System.Diagnostics.ProcessStartInfo.#ctor",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo() instead"),
                new BannedApi("M:System.Diagnostics.ProcessStartInfo.#ctor(System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo() instead"),
                new BannedApi("M:System.Diagnostics.ProcessStartInfo.#ctor(System.String,System.String)",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo() instead"),
                new BannedApi("M:System.Diagnostics.ProcessStartInfo.#ctor(System.String,System.Collections.Generic.IEnumerable{System.String})",
                    ApiCategory.TaskEnvironment, "use TaskEnvironment.GetProcessStartInfo() instead"),

                // ══════════════════════════════════════════════════════════════
                // MSBuildTask0004: Potential issues - review required
                // ══════════════════════════════════════════════════════════════

                // Assembly loading - may cause version conflicts
                new BannedApi("M:System.Reflection.Assembly.LoadFrom(System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.Reflection.Assembly.LoadFile(System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.Reflection.Assembly.Load(System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.Reflection.Assembly.Load(System.Byte[])",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.Reflection.Assembly.Load(System.Byte[],System.Byte[])",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.Reflection.Assembly.LoadWithPartialName(System.String)",
                    ApiCategory.PotentialIssue, "obsolete and may cause version conflicts"),
                new BannedApi("M:System.Activator.CreateInstanceFrom(System.String,System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.Activator.CreateInstance(System.String,System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),

                // AppDomain
                new BannedApi("M:System.AppDomain.Load(System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.AppDomain.Load(System.Byte[])",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.AppDomain.CreateInstanceFrom(System.String,System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host"),
                new BannedApi("M:System.AppDomain.CreateInstance(System.String,System.String)",
                    ApiCategory.PotentialIssue, "may cause version conflicts in shared task host")
            );
        }
    }
}
