// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Diagnostic IDs for the thread-safe task analyzer.
    /// Using a distinct namespace from MSBuild build-time diagnostics (MSBxxxx).
    /// </summary>
    public static class DiagnosticIds
    {
        /// <summary>Critical APIs with no safe alternative (Environment.Exit, Console.*, ThreadPool).</summary>
        public const string CriticalError = "MSBuildTask0001";

        /// <summary>APIs requiring TaskEnvironment alternatives.</summary>
        public const string TaskEnvironmentRequired = "MSBuildTask0002";

        /// <summary>File APIs requiring absolute paths.</summary>
        public const string FilePathRequiresAbsolute = "MSBuildTask0003";

        /// <summary>Potentially problematic APIs (Assembly.Load*, Activator.CreateInstance*).</summary>
        public const string PotentialIssue = "MSBuildTask0004";

        /// <summary>Transitive unsafe API usage detected in task call chain.</summary>
        public const string TransitiveUnsafeCall = "MSBuildTask0005";
    }
}
