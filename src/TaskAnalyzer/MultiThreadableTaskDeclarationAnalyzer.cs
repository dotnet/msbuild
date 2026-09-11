// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Validates that a task's two multithreading declarations agree.
    /// <para>
    /// <c>[MSBuildMultiThreadableTask]</c> is the routing signal: it is the only thing
    /// <c>TaskRouter.NeedsTaskHostInMultiThreadedMode</c> reads, and without it a task runs in an
    /// out-of-proc TaskHost sidecar. <c>IMultiThreadableTask</c> is the injection signal:
    /// <c>TaskExecutionHost</c> assigns <c>TaskEnvironment</c> only to instances of that interface.
    /// </para>
    /// <para>
    /// Declaring one without the other is legal but usually unintended, and both halves fail silently.
    /// The attribute is also reported when it is applied to a type MSBuild never routes as a task --
    /// one that is not a task at all, or an abstract task whose attribute no subclass inherits.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MultiThreadableTaskDeclarationAnalyzer : DiagnosticAnalyzer
    {
        private const string TaskEnvironmentPropertyName = "TaskEnvironment";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(
                DiagnosticDescriptors.TaskEnvironmentNeverAssigned,
                DiagnosticDescriptors.MissingMultiThreadableTaskAttribute,
                DiagnosticDescriptors.MultiThreadableTaskAttributeHasNoEffect);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? taskType = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            INamedTypeSymbol? multiThreadableTaskType =
                context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            INamedTypeSymbol? taskEnvironmentType =
                context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            INamedTypeSymbol? attributeType =
                context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MultiThreadableTaskAttributeFullName);

            if (taskType is null || multiThreadableTaskType is null || taskEnvironmentType is null || attributeType is null)
            {
                return;
            }

            context.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext,
                    taskType,
                    multiThreadableTaskType,
                    taskEnvironmentType,
                    attributeType),
                SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol taskType,
            INamedTypeSymbol multiThreadableTaskType,
            INamedTypeSymbol taskEnvironmentType,
            INamedTypeSymbol attributeType)
        {
            var type = (INamedTypeSymbol)context.Symbol;

            if (type.TypeKind != TypeKind.Class)
            {
                return;
            }

            bool hasAttribute = HasMultiThreadableTaskAttribute(type, attributeType);

            if (!SharedAnalyzerHelpers.ImplementsInterface(type, taskType))
            {
                // TaskRouter only inspects types the engine is about to run as a task, so the routing
                // attribute does nothing here. This usually means it landed on a helper type, or on the
                // wrong class of a multi-class file, leaving the real task unmarked.
                if (hasAttribute)
                {
                    ReportNoEffect(context, type, "it does not implement ITask");
                }

                return;
            }

            // The engine never instantiates an abstract type, and TaskRouter reads the attribute with
            // inherit: false off the concrete type it did instantiate. An attribute here reaches nothing:
            // every derived task is still routed to a TaskHost. Neither MSBuildTask0012 nor
            // MSBuildTask0013 is meaningful on an abstract type either.
            if (type.IsAbstract)
            {
                if (hasAttribute)
                {
                    ReportNoEffect(
                        context,
                        type,
                        "the engine never instantiates an abstract task and the attribute is not inherited, so derived tasks do not pick it up");
                }

                return;
            }

            bool implementsInterface = SharedAnalyzerHelpers.ImplementsInterface(type, multiThreadableTaskType);

            if (hasAttribute && !implementsInterface)
            {
                // The attribute on its own is a supported state: it declares a task safe to run
                // in-process without giving it access to TaskEnvironment. Only report when the task
                // also declares a TaskEnvironment property, which says the author expects the engine
                // to populate it -- and it never will.
                if (HasTaskEnvironmentProperty(type, taskEnvironmentType) &&
                    !HasTaskEnvironmentConstructor(type, taskEnvironmentType))
                {
                    ReportOnType(context, DiagnosticDescriptors.TaskEnvironmentNeverAssigned, type);
                }
            }
            else if (!hasAttribute && DeclaresMultiThreadableTaskInterface(type, multiThreadableTaskType))
            {
                ReportOnType(context, DiagnosticDescriptors.MissingMultiThreadableTaskAttribute, type);
            }
        }

        /// <summary>
        /// Reports whether the type opts into <c>IMultiThreadableTask</c> in its own base list, rather
        /// than merely inheriting it.
        /// <para>
        /// <c>ToolTask</c> implements <c>IMultiThreadableTask</c>, so every <c>ToolTask</c>-derived task in
        /// the ecosystem satisfies the interface without its author having declared anything. Treating an
        /// inherited implementation as intent would report thousands of untouched tasks, which is the same
        /// reason <c>TaskRouter</c> cannot use the interface as a routing signal.
        /// </para>
        /// </summary>
        private static bool DeclaresMultiThreadableTaskInterface(
            INamedTypeSymbol type,
            INamedTypeSymbol multiThreadableTaskType)
        {
            foreach (INamedTypeSymbol declaredInterface in type.Interfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(declaredInterface, multiThreadableTaskType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMultiThreadableTaskAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType)
        {
            // Matches TaskRouter, which reads the attribute with inherit: false.
            foreach (AttributeData attribute in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTaskEnvironmentProperty(INamedTypeSymbol type, INamedTypeSymbol taskEnvironmentType)
        {
            foreach (IPropertySymbol property in SharedAnalyzerHelpers.GetPropertiesIncludingBaseTypes(type))
            {
                if (property.Name == TaskEnvironmentPropertyName &&
                    SymbolEqualityComparer.Default.Equals(property.Type, taskEnvironmentType) &&
                    property.SetMethod is not null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The engine selects a single-parameter <c>TaskEnvironment</c> constructor by signature alone,
        /// independently of <c>IMultiThreadableTask</c>, so such a task does receive an environment.
        /// </summary>
        private static bool HasTaskEnvironmentConstructor(INamedTypeSymbol type, INamedTypeSymbol taskEnvironmentType)
        {
            foreach (IMethodSymbol constructor in type.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public &&
                    constructor.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, taskEnvironmentType))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReportOnType(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, INamedTypeSymbol type)
        {
            foreach (Location location in type.Locations)
            {
                if (location.IsInSource)
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptor, location, type.Name));
                    return;
                }
            }
        }

        /// <summary>
        /// Reports that the routing attribute cannot take effect on this type, naming the reason so the
        /// two shapes -- not a task at all, and an abstract task whose attribute no subclass inherits --
        /// are distinguishable in the message.
        /// </summary>
        private static void ReportNoEffect(SymbolAnalysisContext context, INamedTypeSymbol type, string reason)
        {
            foreach (Location location in type.Locations)
            {
                if (location.IsInSource)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MultiThreadableTaskAttributeHasNoEffect,
                        location,
                        type.Name,
                        reason));
                    return;
                }
            }
        }
    }
}
