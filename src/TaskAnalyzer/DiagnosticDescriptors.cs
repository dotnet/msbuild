// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Diagnostic descriptors for the thread-safe task analyzer.
    /// All rules default to Warning severity. MSBuildTask0001 defaults to Error.
    /// </summary>
    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor CriticalError = new(
            id: DiagnosticIds.CriticalError,
            title: "API is never safe in MSBuild task implementations",
            messageFormat: "'{0}' must not be used in MSBuild tasks: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "This API has no safe alternative in MSBuild tasks. It affects the entire process or interferes with build infrastructure.");

        public static readonly DiagnosticDescriptor TaskEnvironmentRequired = new(
            id: DiagnosticIds.TaskEnvironmentRequired,
            title: "API requires TaskEnvironment alternative in MSBuild tasks",
            messageFormat: "'{0}' should use TaskEnvironment alternative: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This API accesses process-global state. Use the corresponding TaskEnvironment method instead.");

        public static readonly DiagnosticDescriptor FilePathRequiresAbsolute = new(
            id: DiagnosticIds.FilePathRequiresAbsolute,
            title: "File system API requires absolute path in MSBuild tasks",
            messageFormat: "'{0}' may resolve a relative path against the shared working directory: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "File system APIs must receive absolute paths. Use TaskEnvironment.GetAbsolutePath() to convert relative paths.");

        public static readonly DiagnosticDescriptor PotentialIssue = new(
            id: DiagnosticIds.PotentialIssue,
            title: "API may cause issues in multithreaded MSBuild tasks",
            messageFormat: "'{0}' may cause issues in multithreaded tasks: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This API may cause threading issues or version conflicts. Review usage carefully.");

        public static readonly DiagnosticDescriptor TransitiveUnsafeCall = new(
            id: DiagnosticIds.TransitiveUnsafeCall,
            title: "Transitive unsafe API usage in task call chain",
            messageFormat: "'{0}' transitively calls unsafe API '{1}' via: {2}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A method called from this task transitively uses an API that is unsafe in multithreaded task execution. Review the call chain and migrate the callee.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor PreferTypedPathParameter = new(
            id: DiagnosticIds.PreferTypedPathParameter,
            title: "Prefer typed path parameter over manual path construction",
            messageFormat: "Consider changing task property '{0}' from '{1}' to '{2}' instead of converting inside the task body",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "MSBuild can bind AbsolutePath, FileInfo, and DirectoryInfo task parameters automatically for tasks that opt into multithreaded support. Using these types avoids manual path construction in the task body.");

        public static readonly DiagnosticDescriptor PreferTypedTaskItem = new(
            id: DiagnosticIds.PreferTypedTaskItem,
            title: "Prefer ITaskItem<T> over manual ItemSpec parsing",
            messageFormat: "Consider changing task property '{0}' from '{1}' to 'ITaskItem<{2}>{3}' instead of parsing ItemSpec manually",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "MSBuild can bind ITaskItem<T> task parameters that provide a strongly-typed Value property parsed from ItemSpec for tasks that opt into multithreaded support. Using ITaskItem<T> avoids manual parsing in the task body.");

        public static readonly DiagnosticDescriptor InitializeRelativeDefaultInExecute = new(
            id: DiagnosticIds.InitializeRelativeDefaultInExecute,
            title: "Initialize relative default path in Execute()",
            messageFormat: "Task property '{0}' has a relative default path; initialize it in Execute() so it can be rooted through TaskEnvironment when the property is changed to '{1}'",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "A relative default path cannot be rooted in a property initializer because the MSBuild engine only assigns TaskEnvironment after the task is constructed. Move the default into Execute(), where TaskEnvironment.GetAbsolutePath can resolve it, guarding the assignment so a value bound from the project is not overwritten.");

        public static readonly DiagnosticDescriptor UnsupportedTaskItemType = new(
            id: DiagnosticIds.UnsupportedTaskItemType,
            title: "ITaskItem<T> used with unsupported type argument",
            messageFormat: "Task property '{0}' uses ITaskItem<{1}> but MSBuild cannot automatically parse '{1}' from item metadata. Use one of the supported types: {2}.",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MSBuild can only bind ITaskItem<T> properties when T is a supported type. Using an unsupported type will cause a runtime failure when MSBuild tries to bind the parameter.");

        public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
            CriticalError,
            TaskEnvironmentRequired,
            FilePathRequiresAbsolute,
            PotentialIssue,
            TransitiveUnsafeCall,
            PreferTypedPathParameter,
            PreferTypedTaskItem,
            InitializeRelativeDefaultInExecute,
            UnsupportedTaskItemType);
    }
}
