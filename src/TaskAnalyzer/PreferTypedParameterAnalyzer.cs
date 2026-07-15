// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Roslyn analyzer that suggests using strongly-typed task parameters instead of
    /// manual path construction or ItemSpec parsing inside task bodies.
    ///
    /// MSBuildTask0006: Prefer AbsolutePath/FileInfo/DirectoryInfo parameters over converting from string.
    /// MSBuildTask0007: Prefer ITaskItem&lt;T&gt; parameters over parsing ItemSpec manually.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PreferTypedParameterAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(
                DiagnosticDescriptors.PreferTypedPathParameter,
                DiagnosticDescriptors.PreferTypedTaskItem,
                DiagnosticDescriptors.InitializeRelativeDefaultInExecute);

        // Structured metadata carried on each diagnostic so deduplication can reason over the
        // inferred property and suggested type without parsing the human-readable message.
        private const string PropertyNameKey = "PropertyName";
        private const string SuggestedTypeKey = "SuggestedType";

        /// <summary>
        /// The set of types looked up once per compilation and reused for every analyzed task symbol.
        /// <see cref="ITask"/> and <see cref="MultiThreadableTaskAttribute"/> are mandatory; the rest may be
        /// null when the type is not referenced by the compilation, in which case the corresponding
        /// suggestions are simply not produced.
        /// </summary>
        private readonly struct WellKnownTaskTypes
        {
            public WellKnownTaskTypes(
                INamedTypeSymbol iTask,
                INamedTypeSymbol multiThreadableTaskAttribute,
                INamedTypeSymbol? absolutePath,
                INamedTypeSymbol? taskEnvironment,
                INamedTypeSymbol? iTaskItem,
                INamedTypeSymbol? outputAttribute,
                INamedTypeSymbol? fileInfo,
                INamedTypeSymbol? directoryInfo)
            {
                ITask = iTask;
                MultiThreadableTaskAttribute = multiThreadableTaskAttribute;
                AbsolutePath = absolutePath;
                TaskEnvironment = taskEnvironment;
                ITaskItem = iTaskItem;
                OutputAttribute = outputAttribute;
                FileInfo = fileInfo;
                DirectoryInfo = directoryInfo;
            }

            public INamedTypeSymbol ITask { get; }
            public INamedTypeSymbol MultiThreadableTaskAttribute { get; }
            public INamedTypeSymbol? AbsolutePath { get; }
            public INamedTypeSymbol? TaskEnvironment { get; }
            public INamedTypeSymbol? ITaskItem { get; }
            public INamedTypeSymbol? OutputAttribute { get; }
            public INamedTypeSymbol? FileInfo { get; }
            public INamedTypeSymbol? DirectoryInfo { get; }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            if (!TryResolveWellKnownTaskTypes(compilationContext.Compilation, out WellKnownTaskTypes types))
            {
                return;
            }

            var iTaskType = types.ITask;
            var multiThreadableTaskAttributeType = types.MultiThreadableTaskAttribute;
            var absolutePathType = types.AbsolutePath;
            var taskEnvironmentType = types.TaskEnvironment;
            var iTaskItemType = types.ITaskItem;
            var outputAttributeType = types.OutputAttribute;
            var fileInfoType = types.FileInfo;
            var directoryInfoType = types.DirectoryInfo;

            compilationContext.RegisterSymbolStartAction(symbolStartContext =>
            {
                var namedType = (INamedTypeSymbol)symbolStartContext.Symbol;

                // Only multithreadable tasks (ITask + directly-applied [MSBuildMultiThreadableTask]) are analyzed.
                if (!IsMultiThreadableTaskType(namedType, iTaskType, multiThreadableTaskAttributeType))
                {
                    return;
                }

                CollectInputProperties(namedType, iTaskItemType, outputAttributeType,
                    out var stringInputProps, out var taskItemInputProps);

                if (stringInputProps.Count == 0 && taskItemInputProps.Count == 0)
                {
                    return;
                }

                // Collect diagnostics during operation analysis, then deduplicate in SymbolEndAction.
                // This allows suppressing AbsolutePath suggestions when a more specific
                // FileInfo/DirectoryInfo suggestion exists for the same property.
                var pendingDiagnostics = new ConcurrentBag<Diagnostic>();

                symbolStartContext.RegisterOperationAction(ctx =>
                {
                    AnalyzeOperation(ctx, pendingDiagnostics, stringInputProps, taskItemInputProps,
                        absolutePathType, taskEnvironmentType, iTaskItemType,
                        fileInfoType, directoryInfoType);
                },
                OperationKind.ObjectCreation,
                OperationKind.Invocation);

                symbolStartContext.RegisterSymbolEndAction(
                    endCtx => DeduplicateAndReportDiagnostics(endCtx, pendingDiagnostics, stringInputProps));
            }, SymbolKind.NamedType);
        }

        /// <summary>
        /// Resolves overlapping path/item suggestions for each property, redirects relative-default path
        /// properties to MSBuildTask0008, and reports the surviving diagnostics. Runs once per task symbol after
        /// all operations have been analyzed.
        /// </summary>
        private static void DeduplicateAndReportDiagnostics(
            SymbolAnalysisContext endCtx,
            ConcurrentBag<Diagnostic> pendingDiagnostics,
            HashSet<IPropertySymbol> stringInputProps)
        {
            // Group diagnostics by property name + rule ID using structured metadata,
            // then resolve AbsolutePath vs FileInfo/DirectoryInfo overlaps:
            //   - exactly one specific type inferred  => suppress AbsolutePath, keep the specific type
            //   - both FileInfo AND DirectoryInfo     => suppress the specifics, fall back to AbsolutePath
            var diagnosticsList = pendingDiagnostics.ToList();

            var suggestedTypesByPropertyKey = new Dictionary<string, HashSet<string>>(System.StringComparer.Ordinal);
            foreach (var diag in diagnosticsList)
            {
                if (TryGetDiagnosticKey(diag, out string propertyKey, out string suggestedType))
                {
                    if (!suggestedTypesByPropertyKey.TryGetValue(propertyKey, out var suggestedTypes))
                    {
                        suggestedTypes = new HashSet<string>(System.StringComparer.Ordinal);
                        suggestedTypesByPropertyKey[propertyKey] = suggestedTypes;
                    }

                    suggestedTypes.Add(suggestedType);
                }
            }

            // First, determine which path/item diagnostics survive the type-resolution dedup.
            var surviving = new List<Diagnostic>();
            foreach (var diag in diagnosticsList.OrderBy(d => d.Location.SourceSpan.Start))
            {
                if (TryGetDiagnosticKey(diag, out string propertyKey, out string suggestedType) &&
                    suggestedTypesByPropertyKey.TryGetValue(propertyKey, out var suggestedTypes))
                {
                    bool hasFile = suggestedTypes.Contains("FileInfo");
                    bool hasDir = suggestedTypes.Contains("DirectoryInfo");
                    bool hasAbsolutePath = suggestedTypes.Contains("AbsolutePath");

                    if (suggestedType == "AbsolutePath")
                    {
                        // Suppress when exactly one specific type was inferred (no file/dir conflict).
                        if (hasFile ^ hasDir)
                        {
                            continue;
                        }
                    }
                    else if (suggestedType == "FileInfo" || suggestedType == "DirectoryInfo")
                    {
                        // The same value is consumed as both a file and a directory, so neither
                        // specific type is safe — collapse to the AbsolutePath fallback.
                        if (hasFile && hasDir)
                        {
                            if (hasAbsolutePath)
                            {
                                // An AbsolutePath suggestion already exists for this property; drop this
                                // specific one and let the surviving AbsolutePath diagnostics carry it.
                                continue;
                            }

                            // No AbsolutePath site exists, so suppressing both specifics would leave the
                            // property unflagged. Rewrite this diagnostic to AbsolutePath so the property
                            // is still reported with a coherent fallback type.
                            surviving.Add(ToAbsolutePathFallback(diag));
                            continue;
                        }
                    }
                }

                surviving.Add(diag);
            }

            // A path property (MSBuildTask0006) with a relative default cannot be rooted in a property
            // initializer, so instead of the 0006 "retype" suggestion (whose fix is inapplicable here)
            // we redirect it to MSBuildTask0008: initialize the property in Execute() where
            // TaskEnvironment is available. Record such properties, keyed by name, with the resolved type.
            var relativeDefaultProps = new Dictionary<string, (IPropertySymbol Property, string ResolvedType, Location Location)>(System.StringComparer.Ordinal);
            foreach (var diag in surviving)
            {
                if (diag.Id != DiagnosticIds.PreferTypedPathParameter ||
                    !diag.Properties.TryGetValue(PropertyNameKey, out var propName) || propName is null ||
                    !diag.Properties.TryGetValue(SuggestedTypeKey, out var suggestedType) || suggestedType is null ||
                    relativeDefaultProps.ContainsKey(propName))
                {
                    continue;
                }

                var propSymbol = stringInputProps.FirstOrDefault(p => p.Name == propName);
                if (propSymbol is not null &&
                    TryGetRelativeDefaultInitializerLocation(propSymbol, endCtx.CancellationToken, out Location initializerLocation))
                {
                    relativeDefaultProps[propName] = (propSymbol, suggestedType, initializerLocation);
                }
            }

            foreach (var diag in surviving)
            {
                // Suppress the 0006 diagnostics for properties redirected to 0008.
                if (diag.Id == DiagnosticIds.PreferTypedPathParameter &&
                    diag.Properties.TryGetValue(PropertyNameKey, out var pn) && pn is not null &&
                    relativeDefaultProps.ContainsKey(pn))
                {
                    continue;
                }

                endCtx.ReportDiagnostic(diag);
            }

            foreach (var entry in relativeDefaultProps.Values)
            {
                endCtx.ReportDiagnostic(CreateMoveDefaultDiagnostic(entry.Location, entry.Property.Name, entry.ResolvedType));
            }
        }

        /// <summary>
        /// Resolves the types required for analysis. Returns false when the mandatory ITask interface or the
        /// [MSBuildMultiThreadableTask] attribute type is unavailable in the compilation — without either, no
        /// type can be a multithreadable task, so there is nothing to analyze.
        /// </summary>
        private static bool TryResolveWellKnownTaskTypes(Compilation compilation, out WellKnownTaskTypes types)
        {
            types = default;

            // The class must implement ITask to be considered a task at all.
            var iTaskType = compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                return false;
            }

            // The task must additionally opt into multithreaded support by applying the
            // [MSBuildMultiThreadableTask] attribute. Implementing IMultiThreadableTask is not sufficient: the
            // attribute is Inherited = false, so a task that merely derives from a base class implementing the
            // interface has not itself opted into multithreaded support.
            var multiThreadableTaskAttributeType = compilation.GetTypeByMetadataName(WellKnownTypeNames.MultiThreadableTaskAttributeFullName);
            if (multiThreadableTaskAttributeType is null)
            {
                return false;
            }

            types = new WellKnownTaskTypes(
                iTaskType,
                multiThreadableTaskAttributeType,
                compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName),
                compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName),
                compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskItemFullName),
                compilation.GetTypeByMetadataName(WellKnownTypeNames.OutputAttributeFullName),
                compilation.GetTypeByMetadataName(WellKnownTypeNames.FileInfoFullName),
                compilation.GetTypeByMetadataName(WellKnownTypeNames.DirectoryInfoFullName));
            return true;
        }

        /// <summary>
        /// Returns true when <paramref name="namedType"/> implements ITask and directly carries the
        /// [MSBuildMultiThreadableTask] attribute. GetAttributes() returns only directly-applied attributes,
        /// matching the attribute's Inherited = false semantics — a type that merely derives from a
        /// multithreadable base has not itself opted in.
        /// </summary>
        private static bool IsMultiThreadableTaskType(
            INamedTypeSymbol namedType,
            INamedTypeSymbol iTaskType,
            INamedTypeSymbol multiThreadableTaskAttributeType)
        {
            return ImplementsInterface(namedType, iTaskType) &&
                namedType.GetAttributes().Any(
                    attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, multiThreadableTaskAttributeType));
        }

        /// <summary>
        /// Collects the public, settable, non-[Output] input properties of a task — including those declared on
        /// base types — partitioned into string candidates (MSBuildTask0006) and ITaskItem/ITaskItem[]
        /// candidates (MSBuildTask0007).
        /// </summary>
        private static void CollectInputProperties(
            INamedTypeSymbol namedType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? outputAttributeType,
            out HashSet<IPropertySymbol> stringInputProps,
            out HashSet<IPropertySymbol> taskItemInputProps)
        {
            stringInputProps = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
            taskItemInputProps = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);

            // Enumerate input properties declared on this type and all base types (most-derived first). A task
            // can declare its ITaskItem/string inputs on a base class, so limiting to namedType.GetMembers()
            // would miss them. Properties hidden or overridden in a more derived type are processed once, via
            // their most-derived declaration.
            foreach (var member in GetPropertiesIncludingBaseTypes(namedType))
            {
                if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (member.SetMethod is null || member.SetMethod.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                // Exclude [Output] properties — they are set by the task, not MSBuild.
                if (outputAttributeType is not null && member.GetAttributes().Any(
                    a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, outputAttributeType)))
                {
                    continue;
                }

                if (member.Type.SpecialType == SpecialType.System_String)
                {
                    stringInputProps.Add(member);
                }
                else if (iTaskItemType is not null)
                {
                    if (SymbolEqualityComparer.Default.Equals(member.Type, iTaskItemType))
                    {
                        taskItemInputProps.Add(member);
                    }
                    else if (member.Type is IArrayTypeSymbol arrayType &&
                             SymbolEqualityComparer.Default.Equals(arrayType.ElementType, iTaskItemType))
                    {
                        taskItemInputProps.Add(member);
                    }
                }
            }
        }

        private static void AnalyzeOperation(
            OperationAnalysisContext context,
            ConcurrentBag<Diagnostic> pendingDiagnostics,
            HashSet<IPropertySymbol> stringInputProps,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? fileInfoType,
            INamedTypeSymbol? directoryInfoType)
        {
            switch (context.Operation)
            {
                case IObjectCreationOperation creation:
                    AnalyzeObjectCreation(pendingDiagnostics, creation, stringInputProps, taskItemInputProps,
                        absolutePathType, taskEnvironmentType, iTaskItemType, fileInfoType, directoryInfoType);
                    break;

                case IInvocationOperation invocation:
                    AnalyzeInvocation(pendingDiagnostics, invocation, stringInputProps, taskItemInputProps,
                        absolutePathType, taskEnvironmentType, iTaskItemType,
                        fileInfoType, directoryInfoType);
                    break;
            }
        }

        private static bool TryGetDiagnosticKey(Diagnostic diagnostic, out string propertyKey, out string suggestedType)
        {
            if (diagnostic.Properties.TryGetValue(PropertyNameKey, out var propName) && propName is not null &&
                diagnostic.Properties.TryGetValue(SuggestedTypeKey, out var type) && type is not null)
            {
                propertyKey = diagnostic.Id + "|" + propName;
                suggestedType = type;
                return true;
            }

            propertyKey = string.Empty;
            suggestedType = string.Empty;
            return false;
        }

        /// <summary>
        /// If <paramref name="property"/> has exactly one declaration whose initializer is a string literal
        /// holding a relative path, returns true and sets <paramref name="location"/> to the initializer value's
        /// location. Such defaults cannot be rooted in a property initializer and are redirected to
        /// MSBuildTask0008. Only plain string literals are considered (the realistic default form); other
        /// initializer shapes leave the property on the standard 0006 path.
        /// </summary>
        private static bool TryGetRelativeDefaultInitializerLocation(
            IPropertySymbol property,
            System.Threading.CancellationToken cancellationToken,
            out Location location)
        {
            location = Location.None;

            if (property.DeclaringSyntaxReferences.Length != 1 ||
                property.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not PropertyDeclarationSyntax declaration ||
                declaration.Initializer?.Value is not LiteralExpressionSyntax literal ||
                !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return false;
            }

            if (PathDefaultClassifier.IsRelativePathDefault(literal.Token.ValueText))
            {
                location = literal.GetLocation();
                return true;
            }

            return false;
        }

        private static Diagnostic CreateMoveDefaultDiagnostic(Location location, string propertyName, string suggestedType)
        {
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(PropertyNameKey, propertyName)
                .Add(SuggestedTypeKey, suggestedType);

            return Diagnostic.Create(
                DiagnosticDescriptors.InitializeRelativeDefaultInExecute,
                location,
                properties,
                propertyName, suggestedType);
        }

        private static Diagnostic CreatePathDiagnostic(Location location, string propertyName, string suggestedType)
        {
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(PropertyNameKey, propertyName)
                .Add(SuggestedTypeKey, suggestedType);

            return Diagnostic.Create(
                DiagnosticDescriptors.PreferTypedPathParameter,
                location,
                properties,
                propertyName, "string", suggestedType);
        }

        /// <summary>
        /// Rewrites a specific FileInfo/DirectoryInfo path diagnostic into an equivalent AbsolutePath one at
        /// the same location. Used when a property's value is consumed as both a file and a directory (so no
        /// single specific type is safe) and no AbsolutePath site already exists to carry the suggestion.
        /// </summary>
        private static Diagnostic ToAbsolutePathFallback(Diagnostic diagnostic)
        {
            string propertyName = diagnostic.Properties.TryGetValue(PropertyNameKey, out var name) && name is not null
                ? name
                : string.Empty;

            return CreatePathDiagnostic(diagnostic.Location, propertyName, "AbsolutePath");
        }

        private static Diagnostic CreateItemDiagnostic(Location location, string propertyName, bool isArray, string suggestedType)
        {
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(PropertyNameKey, propertyName)
                .Add(SuggestedTypeKey, suggestedType);

            string currentType = isArray ? "ITaskItem[]" : "ITaskItem";
            return Diagnostic.Create(
                DiagnosticDescriptors.PreferTypedTaskItem,
                location,
                properties,
                propertyName, currentType, suggestedType, isArray ? "[]" : "");
        }

        /// <summary>
        /// Reports an <c>ITaskItem&lt;T&gt;</c> suggestion for every distinct task input property whose
        /// ItemSpec (directly, or through an AbsolutePath intermediary) flows into a path-like argument of a
        /// System.IO consumption site (e.g. <c>File.ReadAllLines(...)</c>, <c>Directory.CreateDirectory(...)</c>).
        /// </summary>
        private static void ReportPathConsumers(
            ConcurrentBag<Diagnostic> pendingDiagnostics,
            ImmutableArray<IArgumentOperation> arguments,
            Location location,
            string suggestedType,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType,
            INamedTypeSymbol? taskEnvironmentType)
        {
            var flagged = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
            foreach (var arg in arguments)
            {
                if (arg.Parameter is null || !IsPathParameterName(arg.Parameter.Name))
                {
                    continue;
                }

                var itemProp = FindPathConsumerSource(arg.Value, taskItemInputProps, iTaskItemType, taskEnvironmentType);
                if (itemProp is not null && flagged.Add(itemProp))
                {
                    pendingDiagnostics.Add(CreateItemDiagnostic(
                        location, itemProp.Name, itemProp.Type is IArrayTypeSymbol, suggestedType));
                }
            }
        }

        /// <summary>
        /// Traces a path argument back to a source ITaskItem property, either directly via
        /// <c>item.ItemSpec</c> or through an AbsolutePath intermediary.
        /// </summary>
        private static IPropertySymbol? FindPathConsumerSource(
            IOperation argument,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType,
            INamedTypeSymbol? taskEnvironmentType)
        {
            var (direct, _) = FindItemSpecSource(argument, taskItemInputProps, iTaskItemType);
            if (direct is not null)
            {
                return direct;
            }

            if (taskEnvironmentType is not null)
            {
                return FindItemSpecSourceThroughAbsolutePath(argument, taskItemInputProps, iTaskItemType, taskEnvironmentType);
            }

            return null;
        }

        /// <summary>
        /// Reports an AbsolutePath/FileInfo/DirectoryInfo suggestion (MSBuildTask0006) for every distinct string
        /// task input property whose raw value flows into a path-like argument of a System.IO consumption site
        /// (e.g. <c>File.Delete(prop)</c>, <c>new FileStream(prop, ...)</c>). Arguments that are already safely
        /// wrapped (e.g. <c>GetAbsolutePath(prop)</c>, <c>new AbsolutePath(prop)</c>) are skipped — those are the
        /// resolved forms already handled by the other 0006 branches.
        /// </summary>
        private static void ReportStringPathConsumers(
            ConcurrentBag<Diagnostic> pendingDiagnostics,
            ImmutableArray<IArgumentOperation> arguments,
            Location location,
            string suggestedType,
            HashSet<IPropertySymbol> stringInputProps,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType)
        {
            var flagged = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
            foreach (var arg in arguments)
            {
                if (arg.Parameter is null ||
                    arg.Parameter.Type.SpecialType != SpecialType.System_String ||
                    !IsPathParameterName(arg.Parameter.Name))
                {
                    continue;
                }

                // Only raw (unwrapped) string arguments represent the daisy-chain scenario. A safely-wrapped
                // argument is either already correct or covered by the existing 0006 conversion branches.
                if (IsWrappedSafely(arg.Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    continue;
                }

                var sourceProp = FindSourceProperty(arg.Value, stringInputProps);
                if (sourceProp is not null && flagged.Add(sourceProp))
                {
                    pendingDiagnostics.Add(CreatePathDiagnostic(location, sourceProp.Name, suggestedType));
                }
            }
        }

        private static void AnalyzeObjectCreation(
            ConcurrentBag<Diagnostic> pendingDiagnostics,
            IObjectCreationOperation creation,
            HashSet<IPropertySymbol> stringInputProps,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? fileInfoType,
            INamedTypeSymbol? directoryInfoType)
        {
            var createdType = creation.Type;
            if (createdType is null || creation.Arguments.Length == 0)
            {
                return;
            }

            // MSBuildTask0006: new AbsolutePath(stringProp), new FileInfo(stringProp), new DirectoryInfo(stringProp)
            if (stringInputProps.Count > 0)
            {
                string? suggestedType = null;
                if (absolutePathType is not null && SymbolEqualityComparer.Default.Equals(createdType, absolutePathType))
                {
                    suggestedType = "AbsolutePath";
                }
                else if (fileInfoType is not null && SymbolEqualityComparer.Default.Equals(createdType, fileInfoType))
                {
                    suggestedType = "FileInfo";
                }
                else if (directoryInfoType is not null && SymbolEqualityComparer.Default.Equals(createdType, directoryInfoType))
                {
                    suggestedType = "DirectoryInfo";
                }

                if (suggestedType is not null)
                {
                    var sourceProp = FindSourceProperty(creation.Arguments[0].Value, stringInputProps);
                    if (sourceProp is not null)
                    {
                        pendingDiagnostics.Add(CreatePathDiagnostic(
                            creation.Syntax.GetLocation(), sourceProp.Name, suggestedType));
                        return;
                    }
                }
            }

            // MSBuildTask0007: new AbsolutePath(item.ItemSpec), new FileInfo(item.ItemSpec), new DirectoryInfo(item.ItemSpec)
            if (taskItemInputProps.Count > 0 && iTaskItemType is not null)
            {
                string? suggestedTypeArg = null;
                if (absolutePathType is not null && SymbolEqualityComparer.Default.Equals(createdType, absolutePathType))
                {
                    suggestedTypeArg = "AbsolutePath";
                }
                else if (fileInfoType is not null && SymbolEqualityComparer.Default.Equals(createdType, fileInfoType))
                {
                    suggestedTypeArg = "FileInfo";
                }
                else if (directoryInfoType is not null && SymbolEqualityComparer.Default.Equals(createdType, directoryInfoType))
                {
                    suggestedTypeArg = "DirectoryInfo";
                }

                if (suggestedTypeArg is not null)
                {
                    // Direct: new FileInfo(item.ItemSpec) or new AbsolutePath(item.ItemSpec)
                    var (sourceProp, _) = FindItemSpecSource(creation.Arguments[0].Value, taskItemInputProps, iTaskItemType);
                    if (sourceProp is not null)
                    {
                        pendingDiagnostics.Add(CreateItemDiagnostic(
                            creation.Syntax.GetLocation(), sourceProp.Name, sourceProp.Type is IArrayTypeSymbol, suggestedTypeArg));
                        return;
                    }

                    // Indirect via AbsolutePath: new FileInfo(absLocal) where absLocal = GetAbsolutePath(item.ItemSpec)
                    if (suggestedTypeArg != "AbsolutePath" && taskEnvironmentType is not null)
                    {
                        var itemProp = FindItemSpecSourceThroughAbsolutePath(
                            creation.Arguments[0].Value, taskItemInputProps, iTaskItemType, taskEnvironmentType);
                        if (itemProp is not null)
                        {
                            pendingDiagnostics.Add(CreateItemDiagnostic(
                                creation.Syntax.GetLocation(), itemProp.Name, itemProp.Type is IArrayTypeSymbol, suggestedTypeArg));
                        }
                    }
                }
            }

            // MSBuildTask0007: new FileStream/StreamReader/StreamWriter(item.ItemSpec) consuming an ItemSpec => FileInfo
            if (taskItemInputProps.Count > 0 && iTaskItemType is not null)
            {
                string createdTypeName = createdType.ToDisplayString();
                if (createdTypeName is "System.IO.FileStream" or "System.IO.StreamReader" or "System.IO.StreamWriter")
                {
                    ReportPathConsumers(pendingDiagnostics, creation.Arguments, creation.Syntax.GetLocation(),
                        "FileInfo", taskItemInputProps, iTaskItemType, taskEnvironmentType);
                }
            }

            // MSBuildTask0006: new FileStream/StreamReader/StreamWriter(stringProp) consuming a raw string => FileInfo.
            // Mirrors the ITaskItem consumption above so a raw string path property gets the same one-shot retype.
            if (stringInputProps.Count > 0)
            {
                string createdTypeName = createdType.ToDisplayString();
                if (createdTypeName is "System.IO.FileStream" or "System.IO.StreamReader" or "System.IO.StreamWriter")
                {
                    ReportStringPathConsumers(pendingDiagnostics, creation.Arguments, creation.Syntax.GetLocation(),
                        "FileInfo", stringInputProps, taskEnvironmentType, absolutePathType, iTaskItemType);
                }
            }
        }

        private static void AnalyzeInvocation(
            ConcurrentBag<Diagnostic> pendingDiagnostics,
            IInvocationOperation invocation,
            HashSet<IPropertySymbol> stringInputProps,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? fileInfoType,
            INamedTypeSymbol? directoryInfoType)
        {
            var method = invocation.TargetMethod;

            // MSBuildTask0006: TaskEnvironment.GetAbsolutePath(stringProp)
            if (stringInputProps.Count > 0 &&
                taskEnvironmentType is not null &&
                method.Name == "GetAbsolutePath" &&
                SymbolEqualityComparer.Default.Equals(method.ContainingType, taskEnvironmentType) &&
                invocation.Arguments.Length > 0)
            {
                var sourceProp = FindSourceProperty(invocation.Arguments[0].Value, stringInputProps);
                if (sourceProp is not null)
                {
                    pendingDiagnostics.Add(CreatePathDiagnostic(
                        invocation.Syntax.GetLocation(), sourceProp.Name, "AbsolutePath"));
                    return;
                }
            }

            // MSBuildTask0006: Path.GetFullPath(stringProp). This is the raw normalization pattern that
            // MSBuildTask0002 also flags; surfacing it here too lets the user retype the property in one shot
            // instead of first applying the 0002 fix (which introduces a conversion) and only then seeing 0006.
            if (stringInputProps.Count > 0 &&
                method.Name == "GetFullPath" &&
                method.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                invocation.Arguments.Length > 0)
            {
                var sourceProp = FindSourceProperty(invocation.Arguments[0].Value, stringInputProps);
                if (sourceProp is not null)
                {
                    pendingDiagnostics.Add(CreatePathDiagnostic(
                        invocation.Syntax.GetLocation(), sourceProp.Name, "AbsolutePath"));
                    return;
                }
            }

            // MSBuildTask0007: Parse methods on ItemSpec
            if (taskItemInputProps.Count > 0 && iTaskItemType is not null && invocation.Arguments.Length > 0)
            {
                // Check for Type.Parse(item.ItemSpec) and Convert.ToType(item.ItemSpec)
                string? suggestedTypeArg = GetParsedTypeFromMethod(method);

                // Check for TaskEnvironment.GetAbsolutePath(item.ItemSpec)
                if (suggestedTypeArg is null &&
                    taskEnvironmentType is not null &&
                    method.Name == "GetAbsolutePath" &&
                    SymbolEqualityComparer.Default.Equals(method.ContainingType, taskEnvironmentType))
                {
                    suggestedTypeArg = "AbsolutePath";
                }

                if (suggestedTypeArg is not null)
                {
                    var (sourceProp, _) = FindItemSpecSource(invocation.Arguments[0].Value, taskItemInputProps, iTaskItemType);
                    if (sourceProp is not null)
                    {
                        pendingDiagnostics.Add(CreateItemDiagnostic(
                            invocation.Syntax.GetLocation(), sourceProp.Name, sourceProp.Type is IArrayTypeSymbol, suggestedTypeArg));
                    }
                }
            }

            // MSBuildTask0007: File.X(path)/Directory.X(path) consuming an ItemSpec => FileInfo/DirectoryInfo
            if (taskItemInputProps.Count > 0 && iTaskItemType is not null && invocation.Arguments.Length > 0)
            {
                string? consumerType = method.ContainingType?.ToDisplayString() switch
                {
                    "System.IO.File" => "FileInfo",
                    "System.IO.Directory" => "DirectoryInfo",
                    _ => null
                };

                if (consumerType is not null)
                {
                    ReportPathConsumers(pendingDiagnostics, invocation.Arguments, invocation.Syntax.GetLocation(),
                        consumerType, taskItemInputProps, iTaskItemType, taskEnvironmentType);
                }
            }

            // MSBuildTask0006: File.X(path)/Directory.X(path) consuming a raw string property => FileInfo/DirectoryInfo.
            // This is the raw consumption pattern that MSBuildTask0003 flags; surfacing it here as well lets the
            // user retype the property in one shot instead of daisy-chaining through the 0003 fix first.
            if (stringInputProps.Count > 0 && invocation.Arguments.Length > 0)
            {
                string? consumerType = method.ContainingType?.ToDisplayString() switch
                {
                    "System.IO.File" => "FileInfo",
                    "System.IO.Directory" => "DirectoryInfo",
                    _ => null
                };

                if (consumerType is not null)
                {
                    ReportStringPathConsumers(pendingDiagnostics, invocation.Arguments, invocation.Syntax.GetLocation(),
                        consumerType, stringInputProps, taskEnvironmentType, absolutePathType, iTaskItemType);
                }
            }

            // Path.Combine(...) — only the FIRST argument is safe to suggest as an absolute path.
            // Path.Combine restarts from the last rooted segment: if any argument after the first is an
            // absolute (rooted) path, all preceding segments are discarded. Retyping a non-first argument
            // to AbsolutePath would normalize it to a rooted path and silently change the result
            // (e.g. Path.Combine("C:\\base", "sub") == "C:\\base\\sub", but if "sub" becomes rooted the
            // result changes to that rooted path). The first argument is always the base, so typing it as
            // an absolute path preserves the combine semantics.
            if (method.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                method.Name == "Combine" &&
                invocation.Arguments.Length >= 2)
            {
                IOperation firstArg = invocation.Arguments[0].Value;

                // MSBuildTask0006: Path.Combine(stringProp, ...)
                if (stringInputProps.Count > 0)
                {
                    var sourceProp = FindSourceProperty(firstArg, stringInputProps);
                    if (sourceProp is not null)
                    {
                        pendingDiagnostics.Add(CreatePathDiagnostic(
                            invocation.Syntax.GetLocation(), sourceProp.Name, "AbsolutePath"));
                    }
                }

                // MSBuildTask0007: Path.Combine(item.ItemSpec, ...)
                if (taskItemInputProps.Count > 0 && iTaskItemType is not null)
                {
                    var (itemProp, _) = FindItemSpecSource(firstArg, taskItemInputProps, iTaskItemType);
                    if (itemProp is not null)
                    {
                        pendingDiagnostics.Add(CreateItemDiagnostic(
                            invocation.Syntax.GetLocation(), itemProp.Name, itemProp.Type is IArrayTypeSymbol, "AbsolutePath"));
                    }
                }
            }
        }

        /// <summary>
        /// Determines the parsed target type from a Parse/Convert method call.
        /// Returns the simple type name suitable for ITaskItem&lt;T&gt; suggestion, or null if not a recognized parse call.
        /// Only returns suggestions for types supported by ValueTypeParser.
        /// </summary>
        private static string? GetParsedTypeFromMethod(IMethodSymbol method)
        {
            // Static Parse on value types: int.Parse, bool.Parse, etc.
            // TryParse is deliberately excluded: it is defensive (bool result + out parameter), the suggested
            // ITaskItem<T> would change error handling to a bind-time throw, and its multi-argument shape is
            // never rewritable by the code fix, so flagging it would only produce unfixable false positives.
            // Restrict to types supported by ValueTypeParser to avoid suggesting unsupported types like Guid.
            if (method.Name == "Parse" &&
                method.IsStatic &&
                method.ContainingType is not null &&
                method.ContainingType.IsValueType)
            {
                return SupportedTaskItemTypes.TryGetSpecialTypeDisplayName(
                    method.ContainingType.SpecialType,
                    out string? typeName)
                    ? typeName
                    : null;
            }

            // Convert.ToXxx methods
            if (method.IsStatic &&
                method.ContainingType?.ToDisplayString() == "System.Convert" &&
                method.Name.StartsWith("To", System.StringComparison.Ordinal))
            {
                switch (method.Name)
                {
                    case "ToInt32": return "int";
                    case "ToInt64": return "long";
                    case "ToBoolean": return "bool";
                    case "ToDouble": return "double";
                    case "ToSingle": return "float";
                    case "ToDecimal": return "decimal";
                    case "ToByte": return "byte";
                    case "ToSByte": return "sbyte";
                    case "ToInt16": return "short";
                    case "ToUInt16": return "ushort";
                    case "ToUInt32": return "uint";
                    case "ToUInt64": return "ulong";
                    case "ToChar": return "char";
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the given operation traces back (with at most one level of local indirection)
        /// to one of the collected string task input properties.
        /// </summary>
        private static IPropertySymbol? FindSourceProperty(
            IOperation operation,
            HashSet<IPropertySymbol> candidateProps)
        {
            // Unwrap conversions
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            // Direct property reference: this.MyProp or MyProp
            if (operation is IPropertyReferenceOperation propRef &&
                candidateProps.Contains(propRef.Property))
            {
                return propRef.Property;
            }

            // One level of local indirection: var x = this.MyProp; ... use x ...
            if (operation is ILocalReferenceOperation localRef)
            {
                var local = localRef.Local;
                if (local.DeclaringSyntaxReferences.Length == 1)
                {
                    var syntax = local.DeclaringSyntaxReferences[0].GetSyntax();
                    var semanticModel = operation.SemanticModel;
                    if (semanticModel is not null)
                    {
                        var declOp = semanticModel.GetOperation(syntax);
                        if (declOp is IVariableDeclaratorOperation declarator &&
                            declarator.Initializer?.Value is IOperation initValue)
                        {
                            return FindSourcePropertyDirect(initValue, candidateProps);
                        }
                    }
                }
            }

            // One level of method call wrapping: SomeMethod(stringProp)
            // e.g., FileUtilities.FixFilePath(stringProp)
            if (operation is IInvocationOperation wrappingCall && wrappingCall.Arguments.Length > 0)
            {
                foreach (var arg in wrappingCall.Arguments)
                {
                    var result = FindSourcePropertyDirect(arg.Value, candidateProps);
                    if (result is not null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Direct property reference check without local indirection (prevents infinite recursion).
        /// </summary>
        private static IPropertySymbol? FindSourcePropertyDirect(
            IOperation operation,
            HashSet<IPropertySymbol> candidateProps)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            if (operation is IPropertyReferenceOperation propRef &&
                candidateProps.Contains(propRef.Property))
            {
                return propRef.Property;
            }

            return null;
        }

        /// <summary>
        /// Checks if the given operation is an access to .ItemSpec on an ITaskItem
        /// that traces back to one of the collected task input properties.
        /// Returns the source property and whether it came from an array element.
        /// Also traces through one level of method call wrapping (e.g., FixFilePath(item.ItemSpec)).
        /// </summary>
        private static (IPropertySymbol? sourceProp, bool fromArray) FindItemSpecSource(
            IOperation operation,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType)
        {
            // Unwrap conversions
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            // One level of local indirection for the ItemSpec value:
            // var spec = item.ItemSpec; int.Parse(spec);
            if (operation is ILocalReferenceOperation specLocal)
            {
                var local = specLocal.Local;
                if (local.DeclaringSyntaxReferences.Length == 1)
                {
                    var syntax = local.DeclaringSyntaxReferences[0].GetSyntax();
                    var semanticModel = operation.SemanticModel;
                    if (semanticModel is not null)
                    {
                        var declOp = semanticModel.GetOperation(syntax);
                        if (declOp is IVariableDeclaratorOperation declarator &&
                            declarator.Initializer?.Value is IOperation initValue)
                        {
                            return FindItemSpecSourceDirect(initValue, taskItemInputProps, iTaskItemType);
                        }
                    }
                }

                return (null, false);
            }

            // One level of method call wrapping: SomeMethod(item.ItemSpec)
            // e.g., FileUtilities.FixFilePath(item.ItemSpec)
            if (operation is IInvocationOperation wrappingCall && wrappingCall.Arguments.Length > 0)
            {
                foreach (var arg in wrappingCall.Arguments)
                {
                    var result = FindItemSpecSourceDirect(arg.Value, taskItemInputProps, iTaskItemType);
                    if (result.sourceProp is not null)
                    {
                        return result;
                    }
                }
            }

            return FindItemSpecSourceDirect(operation, taskItemInputProps, iTaskItemType);
        }

        /// <summary>
        /// Direct check: is this operation <c>item.ItemSpec</c> or <c>item.GetMetadata("FullPath")</c> where
        /// item traces to a task property? <c>GetMetadata("FullPath")</c> is the documented way to obtain an
        /// item's absolute path, so it is treated the same as reading the item's path directly.
        /// </summary>
        private static (IPropertySymbol? sourceProp, bool fromArray) FindItemSpecSourceDirect(
            IOperation operation,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            // Check for .ItemSpec property access
            if (operation is IPropertyReferenceOperation itemSpecRef &&
                itemSpecRef.Property.Name == "ItemSpec")
            {
                var receiverType = itemSpecRef.Property.ContainingType;
                if (receiverType is not null &&
                    (SymbolEqualityComparer.Default.Equals(receiverType, iTaskItemType) ||
                     ImplementsInterface(receiverType, iTaskItemType)))
                {
                    // Check if the receiver traces to a task input property
                    var receiver = itemSpecRef.Instance;
                    if (receiver is not null)
                    {
                        return FindTaskItemPropertySource(receiver, taskItemInputProps);
                    }
                }
            }

            // Check for item.GetMetadata("FullPath") — the documented way to get an item's absolute path.
            if (operation is IInvocationOperation getMetadataCall &&
                getMetadataCall.TargetMethod.Name == "GetMetadata" &&
                getMetadataCall.Arguments.Length == 1 &&
                getMetadataCall.Instance is IOperation metadataReceiver &&
                getMetadataCall.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string metadataName } &&
                string.Equals(metadataName, "FullPath", System.StringComparison.OrdinalIgnoreCase))
            {
                var receiverType = getMetadataCall.TargetMethod.ContainingType;
                if (receiverType is not null &&
                    (SymbolEqualityComparer.Default.Equals(receiverType, iTaskItemType) ||
                     ImplementsInterface(receiverType, iTaskItemType)))
                {
                    return FindTaskItemPropertySource(metadataReceiver, taskItemInputProps);
                }
            }

            return (null, false);
        }

        /// <summary>
        /// Traces an ITaskItem receiver back to a task input property,
        /// including through foreach iteration variables and array indexing.
        /// </summary>
        private static (IPropertySymbol? sourceProp, bool fromArray) FindTaskItemPropertySource(
            IOperation operation,
            HashSet<IPropertySymbol> taskItemInputProps)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            // Direct property reference: this.MyItemProp.ItemSpec
            if (operation is IPropertyReferenceOperation propRef &&
                taskItemInputProps.Contains(propRef.Property))
            {
                return (propRef.Property, false);
            }

            // Local variable tracing (foreach variable or simple assignment)
            if (operation is ILocalReferenceOperation localRef)
            {
                var local = localRef.Local;
                var syntaxRef = local.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef is not null)
                {
                    var syntax = syntaxRef.GetSyntax();
                    var semanticModel = operation.SemanticModel;
                    if (semanticModel is not null)
                    {
                        var declOp = semanticModel.GetOperation(syntax);

                        // Simple local assignment: var item = this.MyItemProp;
                        if (declOp is IVariableDeclaratorOperation declarator &&
                            declarator.Initializer?.Value is IOperation initValue)
                        {
                            return FindTaskItemPropertySource(initValue, taskItemInputProps);
                        }

                        // Foreach iteration variable: walk up syntax to find the foreach statement
                        // The variable's declaring syntax may be the identifier token or designation
                        // within a ForEachStatementSyntax
                        var current = syntax;
                        while (current is not null)
                        {
                            var op = semanticModel.GetOperation(current);
                            if (op is IForEachLoopOperation foreachOp)
                            {
                                return FindTaskItemPropertySource(foreachOp.Collection, taskItemInputProps);
                            }

                            current = current.Parent;
                        }
                    }
                }
            }

            // Array element access: this.MyItems[i].ItemSpec
            if (operation is IArrayElementReferenceOperation arrayRef)
            {
                return FindTaskItemPropertySource(arrayRef.ArrayReference, taskItemInputProps);
            }

            return (null, false);
        }

        /// <summary>
        /// Traces through an AbsolutePath intermediary to find the original ITaskItem source.
        /// Handles patterns like:
        ///   AbsolutePath abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec);
        ///   new FileInfo(abs);  // abs traces back to item.ItemSpec
        /// </summary>
        private static IPropertySymbol? FindItemSpecSourceThroughAbsolutePath(
            IOperation operation,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType,
            INamedTypeSymbol taskEnvironmentType)
            => FindItemSpecSourceThroughAbsolutePath(operation, taskItemInputProps, iTaskItemType, taskEnvironmentType, visitedLocals: null);

        private static IPropertySymbol? FindItemSpecSourceThroughAbsolutePath(
            IOperation operation,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType,
            INamedTypeSymbol taskEnvironmentType,
            HashSet<ILocalSymbol>? visitedLocals)
        {
            // Unwrap conversions
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            // Direct: new FileInfo(TaskEnvironment.GetAbsolutePath(item.ItemSpec))
            if (operation is IInvocationOperation invocation &&
                invocation.TargetMethod.Name == "GetAbsolutePath" &&
                SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, taskEnvironmentType) &&
                invocation.Arguments.Length > 0)
            {
                var (sourceProp, _) = FindItemSpecSource(invocation.Arguments[0].Value, taskItemInputProps, iTaskItemType);
                return sourceProp;
            }

            // Local indirection. Handles both:
            //   AbsolutePath abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec); ... use abs ...
            //   AbsolutePath? abs = null; try { abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec); } ... use abs ...
            if (operation is ILocalReferenceOperation localRef)
            {
                var local = localRef.Local;

                // Guard against cycles (e.g. self-referencing assignments).
                visitedLocals ??= new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
                if (!visitedLocals.Add(local))
                {
                    return null;
                }

                // (a) Declaration initializer: AbsolutePath abs = GetAbsolutePath(item.ItemSpec);
                if (local.DeclaringSyntaxReferences.Length == 1 && operation.SemanticModel is SemanticModel declModel)
                {
                    var syntax = local.DeclaringSyntaxReferences[0].GetSyntax();
                    if (declModel.GetOperation(syntax) is IVariableDeclaratorOperation declarator &&
                        declarator.Initializer?.Value is IOperation initValue)
                    {
                        var fromInitializer = FindItemSpecSourceThroughAbsolutePath(
                            initValue, taskItemInputProps, iTaskItemType, taskEnvironmentType, visitedLocals);
                        if (fromInitializer is not null)
                        {
                            return fromInitializer;
                        }
                    }
                }

                // (b) A single unconditional assignment elsewhere in the method body.
                return FindLocalAssignmentSource(localRef, local, taskItemInputProps, iTaskItemType, taskEnvironmentType, visitedLocals);
            }

            return null;
        }

        /// <summary>
        /// Finds the ITaskItem source for a local that is assigned (rather than initialized) via
        /// <c>local = TaskEnvironment.GetAbsolutePath(item.ItemSpec)</c>. Returns the source property only
        /// when every resolvable assignment to the local maps to the same property; otherwise returns null
        /// to stay conservative in the face of ambiguous data flow.
        /// </summary>
        private static IPropertySymbol? FindLocalAssignmentSource(
            ILocalReferenceOperation localRef,
            ILocalSymbol local,
            HashSet<IPropertySymbol> taskItemInputProps,
            INamedTypeSymbol iTaskItemType,
            INamedTypeSymbol taskEnvironmentType,
            HashSet<ILocalSymbol> visitedLocals)
        {
            if (localRef.SemanticModel is not SemanticModel semanticModel ||
                local.ContainingSymbol is not IMethodSymbol method ||
                method.DeclaringSyntaxReferences.Length != 1)
            {
                return null;
            }

            var methodSyntax = method.DeclaringSyntaxReferences[0].GetSyntax();
            var methodOperation = semanticModel.GetOperation(methodSyntax);
            if (methodOperation is null)
            {
                return null;
            }

            IPropertySymbol? found = null;
            foreach (var descendant in methodOperation.Descendants())
            {
                if (descendant is ISimpleAssignmentOperation assignment &&
                    assignment.Target is ILocalReferenceOperation targetRef &&
                    SymbolEqualityComparer.Default.Equals(targetRef.Local, local))
                {
                    var prop = FindItemSpecSourceThroughAbsolutePath(
                        assignment.Value, taskItemInputProps, iTaskItemType, taskEnvironmentType, visitedLocals);
                    if (prop is not null)
                    {
                        if (found is not null && !SymbolEqualityComparer.Default.Equals(found, prop))
                        {
                            // The local is assigned from more than one distinct property — ambiguous.
                            return null;
                        }

                        found = prop;
                    }
                }
            }

            return found;
        }
    }
}
