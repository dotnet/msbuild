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

        /// <summary>Task input property should use AbsolutePath, FileInfo, or DirectoryInfo instead of string.</summary>
        public const string PreferTypedPathParameter = "MSBuildTask0006";

        /// <summary>Task input property should use ITaskItem&lt;T&gt; instead of ITaskItem with manual ItemSpec parsing.</summary>
        public const string PreferTypedTaskItem = "MSBuildTask0007";

        /// <summary>Task input property has a relative default path that should be initialized in Execute() where TaskEnvironment can root it.</summary>
        public const string InitializeRelativeDefaultInExecute = "MSBuildTask0008";

        /// <summary>Task property uses ITaskItem&lt;T&gt; with a type argument not supported by MSBuild's task parameter binder.</summary>
        public const string UnsupportedTaskItemType = "MSBuildTask0009";

        /// <summary>Task property uses ITaskItem&lt;T&gt; with a type argument parsed through Convert.ChangeType.</summary>
        public const string CultureSensitiveTaskItemType = "MSBuildTask0010";

        /// <summary>Task should receive TaskEnvironment through constructor injection.</summary>
        public const string PreferTaskEnvironmentConstructorInjection = "MSBuildTask0011";

        /// <summary>Task declares a TaskEnvironment property but does not implement IMultiThreadableTask, so MSBuild never assigns it.</summary>
        public const string TaskEnvironmentNeverAssigned = "MSBuildTask0012";

        /// <summary>Task implements IMultiThreadableTask but lacks [MSBuildMultiThreadableTask], so it still runs in an out-of-proc TaskHost.</summary>
        public const string MissingMultiThreadableTaskAttribute = "MSBuildTask0013";

        /// <summary>[MSBuildMultiThreadableTask] is applied to a type that is not an MSBuild task, where it has no effect.</summary>
        public const string MultiThreadableTaskAttributeOnNonTask = "MSBuildTask0014";
    }
}
