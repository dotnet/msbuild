// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Roslyn analyzer that reports MSBuildTask0012: a multithreadable task constructs another
    /// <c>ITask</c> without handing it a <c>TaskEnvironment</c>.
    ///
    /// MSBuild only injects <c>TaskEnvironment</c> into the tasks it instantiates itself, so a task
    /// instance created by another task silently falls back to <c>TaskEnvironment.Fallback</c> and
    /// resolves paths and environment variables against the shared process state.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskEnvironmentPropagationAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DiagnosticDescriptors.PropagateTaskEnvironmentToConstructedTask);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            var taskEnvironmentType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            if (iTaskType is null || taskEnvironmentType is null)
            {
                return;
            }

            var iMultiThreadableTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            var multiThreadableTaskAttributeType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MultiThreadableTaskAttributeFullName);

            compilationContext.RegisterSymbolStartAction(symbolStartContext =>
            {
                var namedType = (INamedTypeSymbol)symbolStartContext.Symbol;

                // Only tasks that have a TaskEnvironment of their own can propagate one. Tasks that merely
                // carry [MSBuildMultiThreadableTask] without holding a TaskEnvironment have nothing to pass on.
                if (!IsMultiThreadable(namedType, iMultiThreadableTaskType, multiThreadableTaskAttributeType) ||
                    !HasTaskEnvironmentMember(namedType, taskEnvironmentType))
                {
                    return;
                }

                // Operation actions within a symbol may run concurrently, so both collections must be thread-safe.
                var candidates = new ConcurrentBag<(Location Location, string TypeName, ISymbol? Target)>();
                var receiversWithEnvironment = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);

                symbolStartContext.RegisterOperationAction(
                    operationContext => AnalyzeObjectCreation(operationContext, candidates, iTaskType, taskEnvironmentType),
                    OperationKind.ObjectCreation);

                symbolStartContext.RegisterOperationAction(
                    operationContext => TrackTaskEnvironmentAssignment(operationContext, receiversWithEnvironment, taskEnvironmentType),
                    OperationKind.SimpleAssignment);

                symbolStartContext.RegisterSymbolEndAction(symbolEndContext =>
                {
                    foreach ((Location location, string typeName, ISymbol? target) in candidates)
                    {
                        // A task stored in a local, field, or property may receive its environment through a
                        // later assignment anywhere in the declaring type.
                        if (target is not null && receiversWithEnvironment.ContainsKey(target))
                        {
                            continue;
                        }

                        symbolEndContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.PropagateTaskEnvironmentToConstructedTask,
                            location,
                            typeName));
                    }
                });
            }, SymbolKind.NamedType);
        }

        private static void AnalyzeObjectCreation(
            OperationAnalysisContext context,
            ConcurrentBag<(Location Location, string TypeName, ISymbol? Target)> candidates,
            INamedTypeSymbol iTaskType,
            INamedTypeSymbol taskEnvironmentType)
        {
            var creation = (IObjectCreationOperation)context.Operation;

            if (creation.Type is not INamedTypeSymbol createdType ||
                !ImplementsInterface(createdType, iTaskType) ||
                !CanReceiveTaskEnvironment(createdType, taskEnvironmentType) ||
                ReceivesTaskEnvironment(creation, taskEnvironmentType))
            {
                return;
            }

            candidates.Add((
                creation.Syntax.GetLocation(),
                createdType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                GetCreationTarget(creation)));
        }

        /// <summary>
        /// Records the local, field, or property whose <c>TaskEnvironment</c> is assigned, so a creation
        /// stored in that symbol is not reported.
        /// </summary>
        private static void TrackTaskEnvironmentAssignment(
            OperationAnalysisContext context,
            ConcurrentDictionary<ISymbol, bool> receiversWithEnvironment,
            INamedTypeSymbol taskEnvironmentType)
        {
            var assignment = (ISimpleAssignmentOperation)context.Operation;

            IOperation? instance = assignment.Target switch
            {
                IPropertyReferenceOperation propertyReference when IsTaskEnvironmentType(propertyReference.Property.Type, taskEnvironmentType) => propertyReference.Instance,
                IFieldReferenceOperation fieldReference when IsTaskEnvironmentType(fieldReference.Field.Type, taskEnvironmentType) => fieldReference.Instance,
                _ => null,
            };

            if (GetReferencedSymbol(instance) is ISymbol receiver)
            {
                receiversWithEnvironment[receiver] = true;
            }
        }

        /// <summary>
        /// Checks whether the created task is handed a <c>TaskEnvironment</c> through a constructor
        /// argument or through an object initializer.
        /// </summary>
        private static bool ReceivesTaskEnvironment(IObjectCreationOperation creation, INamedTypeSymbol taskEnvironmentType)
        {
            foreach (IArgumentOperation argument in creation.Arguments)
            {
                if (argument.ArgumentKind != ArgumentKind.DefaultValue &&
                    argument.Parameter is not null &&
                    IsTaskEnvironmentType(argument.Parameter.Type, taskEnvironmentType))
                {
                    return true;
                }
            }

            if (creation.Initializer is IObjectOrCollectionInitializerOperation initializer)
            {
                foreach (IOperation initializerOperation in initializer.Initializers)
                {
                    if (initializerOperation is ISimpleAssignmentOperation assignment &&
                        IsTaskEnvironmentType(assignment.Target.Type, taskEnvironmentType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the local, field, or property the newly created task is stored in, or <see langword="null"/>
        /// when the instance is not stored anywhere this analyzer can track.
        /// </summary>
        private static ISymbol? GetCreationTarget(IObjectCreationOperation creation)
        {
            IOperation? parent = creation.Parent;
            while (parent is IConversionOperation conversion && conversion.IsImplicit)
            {
                parent = conversion.Parent;
            }

            return parent switch
            {
                IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
                IFieldInitializerOperation fieldInitializer => fieldInitializer.InitializedFields.FirstOrDefault(),
                IPropertyInitializerOperation propertyInitializer => propertyInitializer.InitializedProperties.FirstOrDefault(),
                ISimpleAssignmentOperation assignment => GetReferencedSymbol(assignment.Target),
                _ => null,
            };
        }

        private static ISymbol? GetReferencedSymbol(IOperation? operation) => operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local,
            IFieldReferenceOperation fieldReference => fieldReference.Field,
            IPropertyReferenceOperation propertyReference => propertyReference.Property,
            IParameterReferenceOperation parameterReference => parameterReference.Parameter,
            _ => null,
        };

        /// <summary>
        /// Checks whether the constructing type declares or inherits a readable <c>TaskEnvironment</c> member.
        /// </summary>
        private static bool HasTaskEnvironmentMember(INamedTypeSymbol type, INamedTypeSymbol taskEnvironmentType)
        {
            if (GetPropertiesIncludingBaseTypes(type).Any(property =>
                    !property.IsStatic && property.GetMethod is not null && IsTaskEnvironmentType(property.Type, taskEnvironmentType)))
            {
                return true;
            }

            for (INamedTypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field &&
                        IsTaskEnvironmentType(field.Type, taskEnvironmentType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a <c>TaskEnvironment</c> can be handed to the created task at all — through a
        /// settable property or through a constructor parameter. Without either, there is nothing to suggest.
        /// </summary>
        private static bool CanReceiveTaskEnvironment(INamedTypeSymbol createdType, INamedTypeSymbol taskEnvironmentType) =>
            TryGetTaskEnvironmentProperty(createdType, taskEnvironmentType, out _) ||
            createdType.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Any(parameter => IsTaskEnvironmentType(parameter.Type, taskEnvironmentType)));

        /// <summary>
        /// Finds a publicly settable instance property of type <c>TaskEnvironment</c> on the type or one of its bases.
        /// </summary>
        internal static bool TryGetTaskEnvironmentProperty(
            INamedTypeSymbol type,
            INamedTypeSymbol taskEnvironmentType,
            out IPropertySymbol? taskEnvironmentProperty)
        {
            foreach (IPropertySymbol property in GetPropertiesIncludingBaseTypes(type))
            {
                if (!property.IsStatic &&
                    property.DeclaredAccessibility == Accessibility.Public &&
                    property.SetMethod?.DeclaredAccessibility == Accessibility.Public &&
                    IsTaskEnvironmentType(property.Type, taskEnvironmentType))
                {
                    taskEnvironmentProperty = property;
                    return true;
                }
            }

            taskEnvironmentProperty = null;
            return false;
        }

        /// <summary>
        /// Checks whether a type is <c>TaskEnvironment</c> or derives from it.
        /// </summary>
        internal static bool IsTaskEnvironmentType(ITypeSymbol? type, INamedTypeSymbol taskEnvironmentType)
        {
            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, taskEnvironmentType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a type opts into multithreaded task execution, either through
        /// <c>IMultiThreadableTask</c> or through the <c>[MSBuildMultiThreadableTask]</c> attribute.
        /// </summary>
        private static bool IsMultiThreadable(
            INamedTypeSymbol type,
            INamedTypeSymbol? iMultiThreadableTaskType,
            INamedTypeSymbol? multiThreadableTaskAttributeType)
        {
            if (iMultiThreadableTaskType is not null && ImplementsInterface(type, iMultiThreadableTaskType))
            {
                return true;
            }

            if (multiThreadableTaskAttributeType is null)
            {
                return false;
            }

            foreach (AttributeData attribute in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, multiThreadableTaskAttributeType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
