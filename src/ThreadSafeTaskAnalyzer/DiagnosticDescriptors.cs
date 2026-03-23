// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Diagnostic descriptors for the thread-safe task analyzer.
    /// Severity depends on task type:
    ///   - IMultiThreadableTask / [MSBuildMultiThreadableTask]: Warning (default descriptors)
    ///   - Plain ITask without the attribute: Info/Message (Info-suffixed descriptors)
    /// MSBuildTask0001 is always Error regardless of task type.
    /// </summary>
    internal static class DiagnosticDescriptors
    {
        // ── MSBuildTask0001: Always Error ──────────────────────────────────────
        public static readonly DiagnosticDescriptor CriticalError = new(
            id: DiagnosticIds.CriticalError,
            title: "API is never safe in MSBuild task implementations",
            messageFormat: "'{0}' must not be used in MSBuild tasks: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "This API has no safe alternative in MSBuild tasks. It affects the entire process or interferes with build infrastructure.");

        // ── MSBuildTask0002: Warning for multithreadable, Info for plain tasks ─
        public static readonly DiagnosticDescriptor TaskEnvironmentRequired = new(
            id: DiagnosticIds.TaskEnvironmentRequired,
            title: "API requires TaskEnvironment alternative in multithreaded MSBuild tasks",
            messageFormat: "'{0}' should use TaskEnvironment alternative: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This API accesses process-global state. In IMultiThreadableTask implementations, use the corresponding TaskEnvironment method instead.");

        public static readonly DiagnosticDescriptor TaskEnvironmentRequiredInfo = new(
            id: DiagnosticIds.TaskEnvironmentRequired,
            title: "API requires TaskEnvironment alternative in MSBuild tasks",
            messageFormat: "'{0}' should use TaskEnvironment alternative: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "This API accesses process-global state. Use the corresponding TaskEnvironment method when migrating to IMultiThreadableTask.");

        // ── MSBuildTask0003: Warning for multithreadable, Info for plain tasks ─
        public static readonly DiagnosticDescriptor FilePathRequiresAbsolute = new(
            id: DiagnosticIds.FilePathRequiresAbsolute,
            title: "File system API requires absolute path in multithreaded MSBuild tasks",
            messageFormat: "'{0}' may resolve relative paths against the process working directory: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "File system APIs must receive absolute paths. In IMultiThreadableTask implementations, use TaskEnvironment.GetAbsolutePath() to convert relative paths.");

        public static readonly DiagnosticDescriptor FilePathRequiresAbsoluteInfo = new(
            id: DiagnosticIds.FilePathRequiresAbsolute,
            title: "File system API requires absolute path in MSBuild tasks",
            messageFormat: "'{0}' may resolve relative paths against the process working directory: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "File system APIs must receive absolute paths. Use TaskEnvironment.GetAbsolutePath() to convert relative paths when migrating to IMultiThreadableTask.");

        // ── MSBuildTask0004: Warning for multithreadable, Info for plain tasks ─
        public static readonly DiagnosticDescriptor PotentialIssue = new(
            id: DiagnosticIds.PotentialIssue,
            title: "API may cause issues in multithreaded MSBuild tasks",
            messageFormat: "'{0}' may cause issues in multithreaded tasks: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This API may cause threading issues or version conflicts. Review usage carefully.");

        public static readonly DiagnosticDescriptor PotentialIssueInfo = new(
            id: DiagnosticIds.PotentialIssue,
            title: "API may cause issues in MSBuild tasks",
            messageFormat: "'{0}' may cause issues in multithreaded tasks: {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "This API may cause threading issues or version conflicts when migrating to IMultiThreadableTask. Review usage carefully.");

        // ── MSBuildTask0005: Warning for multithreadable, Info for plain tasks ─
        public static readonly DiagnosticDescriptor TransitiveUnsafeCall = new(
            id: DiagnosticIds.TransitiveUnsafeCall,
            title: "Transitive unsafe API usage in multithreaded task call chain",
            messageFormat: "'{0}' transitively calls unsafe API '{1}' via: {2}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A method called from this IMultiThreadableTask transitively uses an API that is unsafe in multithreaded task execution. Review the call chain and migrate the callee.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor TransitiveUnsafeCallInfo = new(
            id: DiagnosticIds.TransitiveUnsafeCall,
            title: "Transitive unsafe API usage in task call chain",
            messageFormat: "'{0}' transitively calls unsafe API '{1}' via: {2}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "A method called from this task transitively uses an API that is unsafe in multithreaded task execution. Review the call chain and migrate the callee when migrating to IMultiThreadableTask.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
            CriticalError,
            TaskEnvironmentRequired,
            TaskEnvironmentRequiredInfo,
            FilePathRequiresAbsolute,
            FilePathRequiresAbsoluteInfo,
            PotentialIssue,
            PotentialIssueInfo,
            TransitiveUnsafeCall,
            TransitiveUnsafeCallInfo);
    }
}
