// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Reports concrete MSBuild task types that do not declare multithreading support (MSBuildTask0012).
    ///
    /// In multithreaded builds the engine routes every task without a directly applied
    /// <c>[MSBuildMultiThreadableTask]</c> attribute to an out-of-proc TaskHost. That is not an error and
    /// produces no diagnostic of its own, so a task added after a repository finished migrating silently
    /// gives back the benefit of the migration. This rule turns that silent regression into a diagnostic.
    ///
    /// The rule reports nothing unless it is opted into, either by setting
    /// <c>msbuild_task_analyzer.scope = require_multithreadable</c> or by configuring
    /// <c>dotnet_diagnostic.MSBuildTask0012.severity</c> explicitly.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RequireMultiThreadableTaskAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The .editorconfig key that configures this rule's severity. Configuring it is an opt-in on its own,
        /// so a repository that prefers per-rule configuration over the scope option is not forced to set both.
        /// </summary>
        private const string SeverityOptionKey = "dotnet_diagnostic." + DiagnosticIds.RequireMultiThreadableTask + ".severity";

        private const string SeverityNone = "none";
        private const string SeverityDefault = "default";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DiagnosticDescriptors.RequireMultiThreadableTask);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? iTaskType = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                return;
            }

            // The scope option and a ruleset / <WarningsAsErrors> entry are compilation-wide, so they are read
            // once here. A severity set through .editorconfig can vary per file and is read only for a type that
            // would otherwise be reported.
            bool optedIn = SharedAnalyzerHelpers.ReadRequireMultiThreadableOption(context.Options.AnalyzerConfigOptionsProvider) ||
                IsEnabledByCompilationOptions(context.Compilation.Options);

            context.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, iTaskType, optedIn), SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol iTaskType, bool optedIn)
        {
            var taskType = (INamedTypeSymbol)context.Symbol;

            // The attribute is not inherited, so only concrete types are asked to declare support: an abstract
            // base cannot opt in on behalf of the types deriving from it.
            if (taskType.TypeKind != TypeKind.Class ||
                taskType.IsAbstract ||
                !SharedAnalyzerHelpers.ImplementsInterface(taskType, iTaskType) ||
                HasMultiThreadableTaskAttribute(taskType))
            {
                return;
            }

            foreach (Location location in taskType.Locations)
            {
                if (!location.IsInSource)
                {
                    continue;
                }

                if (optedIn || IsEnabledByAnalyzerConfig(context.Options.AnalyzerConfigOptionsProvider, location.SourceTree))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.RequireMultiThreadableTask,
                        location,
                        taskType.Name));
                }

                return;
            }
        }

        /// <summary>
        /// Returns true when the type directly carries <c>Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute</c>.
        /// The attribute is matched by namespace and name rather than by symbol identity, mirroring how the engine
        /// detects it, so a task marked with a copy of the attribute defined in its own assembly is not reported.
        /// <see cref="ISymbol.GetAttributes"/> returns only directly applied attributes, matching the attribute's
        /// <c>Inherited = false</c> semantics.
        /// </summary>
        private static bool HasMultiThreadableTaskAttribute(INamedTypeSymbol taskType)
        {
            foreach (AttributeData attribute in taskType.GetAttributes())
            {
                INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                if (attributeClass is not null &&
                    string.Equals(attributeClass.Name, WellKnownTypeNames.MultiThreadableTaskAttributeName, StringComparison.Ordinal) &&
                    string.Equals(attributeClass.ContainingNamespace?.ToDisplayString(), WellKnownTypeNames.FrameworkNamespace, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when a ruleset, <c>&lt;WarningsAsErrors&gt;</c>, or an equivalent compiler switch enables the rule.
        /// </summary>
        private static bool IsEnabledByCompilationOptions(CompilationOptions compilationOptions) =>
            compilationOptions.SpecificDiagnosticOptions.TryGetValue(DiagnosticIds.RequireMultiThreadableTask, out ReportDiagnostic report) &&
            report != ReportDiagnostic.Suppress &&
            report != ReportDiagnostic.Default;

        /// <summary>
        /// Returns true when <c>dotnet_diagnostic.MSBuildTask0012.severity</c> is configured for the given tree, or
        /// globally, to anything other than "none". Roslyn applies the configured severity to what is reported; this
        /// only decides whether the rule was opted into at all.
        /// </summary>
        private static bool IsEnabledByAnalyzerConfig(AnalyzerConfigOptionsProvider optionsProvider, SyntaxTree? tree)
        {
            if ((tree is not null && optionsProvider.GetOptions(tree).TryGetValue(SeverityOptionKey, out string? severity)) ||
                optionsProvider.GlobalOptions.TryGetValue(SeverityOptionKey, out severity))
            {
                return !string.Equals(severity, SeverityNone, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(severity, SeverityDefault, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
