// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
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
                DiagnosticDescriptors.PreferTypedTaskItem);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            // The class must implement ITask to be considered a task at all
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                return;
            }

            // Additionally, the task must opt into multithreaded support
            var iMultiThreadableTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            var multiThreadableTaskAttributeType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MultiThreadableTaskAttributeFullName);
            if (iMultiThreadableTaskType is null && multiThreadableTaskAttributeType is null)
            {
                return;
            }

            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName);
            var taskEnvironmentType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            var iTaskItemType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskItemFullName);
            var outputAttributeType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.OutputAttributeFullName);
            var fileInfoType = compilationContext.Compilation.GetTypeByMetadataName("System.IO.FileInfo");
            var directoryInfoType = compilationContext.Compilation.GetTypeByMetadataName("System.IO.DirectoryInfo");

            compilationContext.RegisterSymbolStartAction(symbolStartContext =>
            {
                var namedType = (INamedTypeSymbol)symbolStartContext.Symbol;

                // Gate 1: Must be an ITask implementation
                if (!ImplementsInterface(namedType, iTaskType))
                {
                    return;
                }

                // Gate 2: Must have opted into multithreaded support
                bool isMultiThreadable =
                    (iMultiThreadableTaskType is not null && ImplementsInterface(namedType, iMultiThreadableTaskType)) ||
                    (multiThreadableTaskAttributeType is not null && namedType.GetAttributes().Any(
                        attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, multiThreadableTaskAttributeType)));

                if (!isMultiThreadable)
                {
                    return;
                }

                // Collect string-typed task input properties (candidates for MSBuildTask0006)
                var stringInputProps = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
                // Collect ITaskItem/ITaskItem[]-typed task input properties (candidates for MSBuildTask0007)
                var taskItemInputProps = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);

                foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    if (member.SetMethod is null || member.SetMethod.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    // Exclude [Output] properties — they are set by the task, not MSBuild
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

                if (stringInputProps.Count == 0 && taskItemInputProps.Count == 0)
                {
                    return;
                }

                symbolStartContext.RegisterOperationAction(ctx =>
                {
                    AnalyzeOperation(ctx, stringInputProps, taskItemInputProps,
                        absolutePathType, taskEnvironmentType, iTaskItemType,
                        fileInfoType, directoryInfoType);
                },
                OperationKind.ObjectCreation,
                OperationKind.Invocation);
            }, SymbolKind.NamedType);
        }

        private static void AnalyzeOperation(
            OperationAnalysisContext context,
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
                    AnalyzeObjectCreation(context, creation, stringInputProps, taskItemInputProps,
                        absolutePathType, taskEnvironmentType, iTaskItemType, fileInfoType, directoryInfoType);
                    break;

                case IInvocationOperation invocation:
                    AnalyzeInvocation(context, invocation, stringInputProps, taskItemInputProps,
                        absolutePathType, taskEnvironmentType, iTaskItemType,
                        fileInfoType, directoryInfoType);
                    break;
            }
        }

        private static void AnalyzeObjectCreation(
            OperationAnalysisContext context,
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
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.PreferTypedPathParameter,
                            creation.Syntax.GetLocation(),
                            sourceProp.Name, "string", suggestedType));
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
                        string currentType = sourceProp.Type is IArrayTypeSymbol ? "ITaskItem[]" : "ITaskItem";
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.PreferTypedTaskItem,
                            creation.Syntax.GetLocation(),
                            sourceProp.Name, currentType, suggestedTypeArg));
                        return;
                    }

                    // Indirect via AbsolutePath: new FileInfo(absLocal) where absLocal = GetAbsolutePath(item.ItemSpec)
                    if (suggestedTypeArg != "AbsolutePath" && taskEnvironmentType is not null)
                    {
                        var itemProp = FindItemSpecSourceThroughAbsolutePath(
                            creation.Arguments[0].Value, taskItemInputProps, iTaskItemType, taskEnvironmentType);
                        if (itemProp is not null)
                        {
                            string currentType = itemProp.Type is IArrayTypeSymbol ? "ITaskItem[]" : "ITaskItem";
                            context.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.PreferTypedTaskItem,
                                creation.Syntax.GetLocation(),
                                itemProp.Name, currentType, suggestedTypeArg));
                        }
                    }
                }
            }
        }

        private static void AnalyzeInvocation(
            OperationAnalysisContext context,
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
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PreferTypedPathParameter,
                        invocation.Syntax.GetLocation(),
                        sourceProp.Name, "string", "AbsolutePath"));
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
                        string currentType = sourceProp.Type is IArrayTypeSymbol ? "ITaskItem[]" : "ITaskItem";
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.PreferTypedTaskItem,
                            invocation.Syntax.GetLocation(),
                            sourceProp.Name, currentType, suggestedTypeArg));
                    }
                }
            }

            // Path.Combine(...) with any argument tracing to a task input property
            if (method.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                method.Name == "Combine" &&
                invocation.Arguments.Length >= 2)
            {
                foreach (var arg in invocation.Arguments)
                {
                    // MSBuildTask0006: Path.Combine(stringProp, ...) or Path.Combine(..., stringProp)
                    if (stringInputProps.Count > 0)
                    {
                        var sourceProp = FindSourceProperty(arg.Value, stringInputProps);
                        if (sourceProp is not null)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.PreferTypedPathParameter,
                                invocation.Syntax.GetLocation(),
                                sourceProp.Name, "string", "AbsolutePath"));
                            break;
                        }
                    }

                    // MSBuildTask0007: Path.Combine(item.ItemSpec, ...) or Path.Combine(..., item.ItemSpec)
                    if (taskItemInputProps.Count > 0 && iTaskItemType is not null)
                    {
                        var (itemProp, _) = FindItemSpecSource(arg.Value, taskItemInputProps, iTaskItemType);
                        if (itemProp is not null)
                        {
                            string currentType = itemProp.Type is IArrayTypeSymbol ? "ITaskItem[]" : "ITaskItem";
                            context.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.PreferTypedTaskItem,
                                invocation.Syntax.GetLocation(),
                                itemProp.Name, currentType, "AbsolutePath"));
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines the parsed target type from a Parse/TryParse/Convert method call.
        /// Returns the simple type name suitable for ITaskItem&lt;T&gt; suggestion, or null if not a recognized parse call.
        /// </summary>
        private static string? GetParsedTypeFromMethod(IMethodSymbol method)
        {
            // Static Parse/TryParse on value types: int.Parse, bool.Parse, etc.
            if ((method.Name == "Parse" || method.Name == "TryParse") &&
                method.IsStatic &&
                method.ContainingType is not null &&
                method.ContainingType.IsValueType)
            {
                return method.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
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
        /// Direct check: is this operation item.ItemSpec where item traces to a task property?
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

            // Local indirection: var abs = TaskEnvironment.GetAbsolutePath(item.ItemSpec); new FileInfo(abs);
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
                            return FindItemSpecSourceThroughAbsolutePath(
                                initValue, taskItemInputProps, iTaskItemType, taskEnvironmentType);
                        }
                    }
                }
            }

            return null;
        }
    }
}
