// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Diagnostic descriptors for the thread-safe task analyzer.
    /// Rules default to the severity appropriate for the behavior they diagnose.
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
            messageFormat: "Task property '{0}' uses ITaskItem<{1}> but MSBuild cannot automatically parse '{1}' from item metadata. Use one of the directly parsed types: {2}.",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MSBuild can only bind ITaskItem<T> properties when T is a supported type. Using an unsupported type will cause a runtime failure when MSBuild tries to bind the parameter.");

        public static readonly DiagnosticDescriptor CultureSensitiveTaskItemType = new(
            id: DiagnosticIds.CultureSensitiveTaskItemType,
            title: "ITaskItem<T> type argument relies on culture-sensitive conversion",
            messageFormat: "Task property '{0}' uses ITaskItem<{1}>, which MSBuild parses through Convert.ChangeType using CultureInfo.InvariantCulture. Use ITaskItem<string> and parse explicitly with a chosen culture.",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ITaskItem<T> type arguments parsed through Convert.ChangeType use CultureInfo.InvariantCulture. Bind the item as a string and parse it explicitly with the intended culture.");

        public static readonly DiagnosticDescriptor PreferTaskEnvironmentConstructorInjection = new(
            id: DiagnosticIds.PreferTaskEnvironmentConstructorInjection,
            title: "Prefer constructor injection for TaskEnvironment",
            messageFormat: "Task '{0}' receives TaskEnvironment only after construction; add a public constructor with a single TaskEnvironment parameter to make it available during construction",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Constructor injection makes TaskEnvironment available to constructor logic and environment-dependent default initialization. The MSBuild engine prefers a public constructor with a single TaskEnvironment parameter when one is available.");

        public static readonly DiagnosticDescriptor TaskEnvironmentNeverAssigned = new(
            id: DiagnosticIds.TaskEnvironmentNeverAssigned,
            title: "TaskEnvironment property is never assigned by MSBuild because the task does not implement IMultiThreadableTask",
            messageFormat: "Task '{0}' declares a TaskEnvironment property but does not implement IMultiThreadableTask, so MSBuild never assigns it and it retains the task's own default; implement IMultiThreadableTask, or add a public constructor taking a TaskEnvironment that assigns the property",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "MSBuild assigns TaskEnvironment only to tasks that implement IMultiThreadableTask, or through a public constructor that takes a single TaskEnvironment. A task that declares the property without either never receives an environment from the engine: the property silently retains whatever the task itself initialized it to -- commonly TaskEnvironment.Fallback, or null when there is no initializer -- so paths resolve against the shared process working directory rather than the project directory. Because the task carries [MSBuildMultiThreadableTask] it runs in-process, which is exactly where that resolution is wrong.");

        public static readonly DiagnosticDescriptor MissingMultiThreadableTaskAttribute = new(
            id: DiagnosticIds.MissingMultiThreadableTaskAttribute,
            title: "Task implements IMultiThreadableTask but is not marked with [MSBuildMultiThreadableTask]",
            messageFormat: "Task '{0}' implements IMultiThreadableTask but is not marked with [MSBuildMultiThreadableTask], so it still runs in an out-of-proc TaskHost",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: false,
            description: "Only [MSBuildMultiThreadableTask] causes a task to run in-process in multithreaded mode; IMultiThreadableTask controls TaskEnvironment injection alone. Implementing the interface without the attribute is a valid intermediate state -- the task resolves paths correctly while remaining isolated in a TaskHost -- so this rule is disabled by default. Enable it once a codebase intends every multithreadable task to also be routed in-process. Only a type that declares the interface in its own base list is reported: ToolTask implements IMultiThreadableTask, so an inherited implementation says nothing about the derived task's intent.");

        public static readonly DiagnosticDescriptor MultiThreadableTaskAttributeHasNoEffect = new(
            id: DiagnosticIds.MultiThreadableTaskAttributeHasNoEffect,
            title: "[MSBuildMultiThreadableTask] has no effect on this type",
            messageFormat: "[MSBuildMultiThreadableTask] on type '{0}' has no effect because {1}",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "TaskRouter reads [MSBuildMultiThreadableTask] with inherit: false, off the concrete type the engine has just instantiated as a task. The attribute therefore only has an effect on a non-abstract class that implements ITask. On a type that is not a task, nothing ever reads it. On an abstract task, the engine never instantiates that type, and because the attribute is not inherited the concrete subclasses do not pick it up -- so every one of them is still routed to an out-of-proc TaskHost. Both shapes usually mean the attribute was applied to the wrong class: a helper type beside the real task, or a shared base instead of each task that derives from it.");

        public static readonly DiagnosticDescriptor RequireMultiThreadableTask = new(
            id: DiagnosticIds.RequireMultiThreadableTask,
            title: "Concrete MSBuild task type does not opt into multithreaded execution",
            messageFormat: "Task '{0}' does not declare multithreading support; apply [MSBuildMultiThreadableTask] so it is not routed to an out-of-proc TaskHost",
            category: "MSBuild.TaskAuthoring",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "In multithreaded builds, a task without the [MSBuildMultiThreadableTask] attribute runs in an out-of-proc TaskHost, which succeeds but is slow. The attribute is not inherited, so deriving from a migrated base class is not enough. This rule reports nothing unless it is opted into, either with 'msbuild_task_analyzer.scope = require_multithreadable' or by configuring its severity explicitly; a repository that has finished migrating its tasks opts in to keep new tasks from silently regressing. It covers every concrete task type, so it subsumes MSBuildTask0013, which reports the same missing attribute only on the narrower set of tasks that declare IMultiThreadableTask; a repository that opts into this rule does not also need to enable that one.");

        public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
            CriticalError,
            TaskEnvironmentRequired,
            FilePathRequiresAbsolute,
            PotentialIssue,
            TransitiveUnsafeCall,
            PreferTypedPathParameter,
            PreferTypedTaskItem,
            InitializeRelativeDefaultInExecute,
            UnsupportedTaskItemType,
            CultureSensitiveTaskItemType,
            PreferTaskEnvironmentConstructorInjection,
            TaskEnvironmentNeverAssigned,
            MissingMultiThreadableTaskAttribute,
            MultiThreadableTaskAttributeHasNoEffect,
            RequireMultiThreadableTask);
    }
}
