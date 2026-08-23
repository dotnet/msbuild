// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
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
            // The scope option, a ruleset / <WarningsAsErrors> entry, and a .globalconfig severity are
            // compilation-wide, so they are read once here. A severity set through .editorconfig can vary per file,
            // so when nothing opted in compilation-wide the trees are scanned once for a per-file opt-in, and the
            // tree of a type that would otherwise be reported is then checked individually.
            bool optedIn = SharedAnalyzerHelpers.ReadRequireMultiThreadableOption(context.Options.AnalyzerConfigOptionsProvider) ||
                IsEnabledForCompilation(context.Compilation, context.CancellationToken);

            // No type is examined when the rule is not opted into anywhere in the compilation, so a repository that
            // has not migrated pays nothing for the rule.
            if (!optedIn && !IsEnabledForAnyTree(context.Compilation, context.CancellationToken))
            {
                return;
            }

            INamedTypeSymbol? iTaskType = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                return;
            }

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

            // A partial type can be declared in several files, and .editorconfig can enable the rule for some of
            // them only, so the first declaration the rule is enabled for is the one reported.
            foreach (Location location in taskType.Locations)
            {
                if (location.IsInSource &&
                    (optedIn || IsEnabledForTree(context.Compilation, location.SourceTree, context.CancellationToken)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.RequireMultiThreadableTask,
                        location,
                        taskType.Name));
                    return;
                }
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
        /// Returns true when the rule's severity is configured for the whole compilation, either by a ruleset,
        /// <c>&lt;WarningsAsErrors&gt;</c> and friends, or by <c>dotnet_diagnostic.MSBuildTask0012.severity</c> in a
        /// .globalconfig. Configuring the severity is an opt-in on its own, so a repository that prefers per-rule
        /// configuration over the scope option is not forced to set both.
        /// </summary>
        private static bool IsEnabledForCompilation(Compilation compilation, CancellationToken cancellationToken)
        {
            if (compilation.Options.SpecificDiagnosticOptions.TryGetValue(DiagnosticIds.RequireMultiThreadableTask, out ReportDiagnostic severity))
            {
                return IsEnabled(severity);
            }

            SyntaxTreeOptionsProvider? optionsProvider = compilation.Options.SyntaxTreeOptionsProvider;
            return optionsProvider is not null &&
                optionsProvider.TryGetGlobalDiagnosticValue(DiagnosticIds.RequireMultiThreadableTask, cancellationToken, out severity) &&
                IsEnabled(severity);
        }

        /// <summary>
        /// Returns true when <c>dotnet_diagnostic.MSBuildTask0012.severity</c> is configured for any file in the
        /// compilation, which is how a repository enables the rule for part of its sources only.
        /// </summary>
        private static bool IsEnabledForAnyTree(Compilation compilation, CancellationToken cancellationToken)
        {
            if (compilation.Options.SyntaxTreeOptionsProvider is null)
            {
                return false;
            }

            foreach (SyntaxTree tree in compilation.SyntaxTrees)
            {
                if (IsEnabledForTree(compilation, tree, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when <c>dotnet_diagnostic.MSBuildTask0012.severity</c> is configured for the given tree.
        /// </summary>
        private static bool IsEnabledForTree(Compilation compilation, SyntaxTree? tree, CancellationToken cancellationToken)
        {
            SyntaxTreeOptionsProvider? optionsProvider = compilation.Options.SyntaxTreeOptionsProvider;
            return tree is not null &&
                optionsProvider is not null &&
                optionsProvider.TryGetDiagnosticValue(tree, DiagnosticIds.RequireMultiThreadableTask, cancellationToken, out ReportDiagnostic severity) &&
                IsEnabled(severity);
        }

        /// <summary>
        /// Roslyn applies the configured severity to what is reported; this only decides whether the rule was opted
        /// into at all, so any severity other than "none" and "default" counts.
        /// </summary>
        private static bool IsEnabled(ReportDiagnostic severity) =>
            severity is not ReportDiagnostic.Suppress and not ReportDiagnostic.Default;
    }
}
