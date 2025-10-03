// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

#pragma warning disable RS1012 // Start action has no registered actions
#pragma warning disable RS2008 // Start action has no registered actions
namespace Microsoft.Build.Utilities.Analyzer
{
    /// <summary>
    /// Analyzer that bans APIs when used within implementations of IMultiThreadableTask.
    /// Multithreadable tasks are run in parallel and must not use APIs that depend on process-global state
    /// such as current working directory, environment variables, or process-wide culture settings.
    /// </summary>
    public abstract class IMultiThreadableTaskBannedAnalyzer<TSyntaxKind> : DiagnosticAnalyzer
        where TSyntaxKind : struct
    {
        private const string IMultiThreadableTaskInterfaceName = "Microsoft.Build.Framework.IMultiThreadableTask";

        /// <summary>
        /// MSB9999: Critical errors - APIs that are never safe in multithreaded tasks.
        /// </summary>
        public static readonly DiagnosticDescriptor CriticalErrorRule = new DiagnosticDescriptor(
            id: "MSB9999",
            title: "API is never safe in IMultiThreadableTask implementations",
            messageFormat: "Symbol '{0}' is banned in IMultiThreadableTask implementations{1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "This API has no safe alternative in multithreaded tasks and affects the entire process (e.g., Environment.Exit, ThreadPool settings, CultureInfo defaults).");

        /// <summary>
        /// MSB9998: TaskEnvironment required - APIs that must use TaskEnvironment instead.
        /// </summary>
        public static readonly DiagnosticDescriptor TaskEnvironmentRequiredRule = new DiagnosticDescriptor(
            id: "MSB9998",
            title: "API requires TaskEnvironment alternative in IMultiThreadableTask implementations",
            messageFormat: "Symbol '{0}' requires TaskEnvironment alternative{1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This API accesses process-global state and must use TaskEnvironment alternatives (e.g., TaskEnvironment.ProjectCurrentDirectory, GetEnvironmentVariable, GetAbsolutePath).");

        /// <summary>
        /// MSB9997: File path APIs that need absolute paths.
        /// </summary>
        public static readonly DiagnosticDescriptor FilePathRequiresAbsoluteRule = new DiagnosticDescriptor(
            id: "MSB9997",
            title: "File path API requires absolute path in IMultiThreadableTask implementations",
            messageFormat: "Symbol '{0}' requires absolute path{1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "File system APIs must use absolute paths. Wrap path arguments with TaskEnvironment.GetAbsolutePath() to ensure thread-safe path resolution.");

        /// <summary>
        /// MSB9996: Potentially problematic APIs that require case-by-case review.
        /// </summary>
        public static readonly DiagnosticDescriptor PotentialIssueRule = new DiagnosticDescriptor(
            id: "MSB9996",
            title: "API may cause threading issues in IMultiThreadableTask implementations",
            messageFormat: "Symbol '{0}' may cause threading issues{1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This API may cause threading issues (e.g., Console I/O, Assembly.Load). Review usage carefully for thread-safety.");

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(CriticalErrorRule, TaskEnvironmentRequiredRule, FilePathRequiresAbsoluteRule, PotentialIssueRule);

        protected abstract SymbolDisplayFormat SymbolDisplayFormat { get; }
        protected abstract bool IsTypeDeclaration(SyntaxNode node);

        /// <summary>
        /// Gets types whose methods with string path parameters should be checked.
        /// These are safe IF called with absolute paths (wrapped with TaskEnvironment.GetAbsolutePath).
        /// </summary>
        private static ImmutableArray<string> GetTypesWithPathMethods()
        {
            return ImmutableArray.Create(
                "System.IO.File",
                "System.IO.Directory",
                "System.IO.FileInfo",
                "System.IO.DirectoryInfo",
                "System.IO.FileStream",
                "System.IO.StreamReader",
                "System.IO.StreamWriter");
        }

        /// <summary>
        /// Categorizes banned APIs by diagnostic code.
        /// </summary>
        private enum ApiCategory
        {
            CriticalError,      // MSB9999
            TaskEnvironment,    // MSB9998
            PotentialIssue      // MSB9996
        }

        /// <summary>
        /// Gets the hardcoded list of APIs with their category and message.
        /// File path APIs (MSB9997) are handled separately via pattern matching.
        /// </summary>
        private static ImmutableArray<(string DeclarationId, ApiCategory Category, string Message)> GetBannedApiDefinitions()
        {
            return ImmutableArray.Create(
                // MSB9998: Path.GetFullPath - use TaskEnvironment.GetAbsolutePath
                ("M:System.IO.Path.GetFullPath(System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetAbsolutePath instead"),
                ("M:System.IO.Path.GetFullPath(System.String,System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetAbsolutePath instead"),

                // MSB9998: Environment - use TaskEnvironment alternatives
                ("P:System.Environment.CurrentDirectory", ApiCategory.TaskEnvironment, "Use TaskEnvironment.ProjectCurrentDirectory instead"),
                ("M:System.Environment.GetEnvironmentVariable(System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetEnvironmentVariable instead"),
                ("M:System.Environment.SetEnvironmentVariable(System.String,System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.SetEnvironmentVariable instead"),
                ("M:System.Environment.SetEnvironmentVariable(System.String,System.String,System.EnvironmentVariableTarget)", ApiCategory.TaskEnvironment, "Drop target parameter - no direct TaskEnvironment equivalent"),

                // MSB9999: Critical - Process termination
                ("M:System.Environment.Exit(System.Int32)", ApiCategory.CriticalError, "Terminates entire process - return false from task or throw exception instead"),
                ("M:System.Environment.FailFast(System.String)", ApiCategory.CriticalError, "Terminates entire process - return false from task or throw exception instead"),
                ("M:System.Environment.FailFast(System.String,System.Exception)", ApiCategory.CriticalError, "Terminates entire process - return false from task or throw exception instead"),
                ("M:System.Environment.FailFast(System.String,System.Exception,System.String)", ApiCategory.CriticalError, "Terminates entire process - return false from task or throw exception instead"),

                // MSB9999: Critical - Process control
                ("M:System.Diagnostics.Process.Kill", ApiCategory.CriticalError, "Terminates process"),
                ("M:System.Diagnostics.Process.Kill(System.Boolean)", ApiCategory.CriticalError, "Terminates process"),

                // MSB9998: Process.Start - use TaskEnvironment.GetProcessStartInfo
                ("M:System.Diagnostics.Process.Start(System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),
                ("M:System.Diagnostics.Process.Start(System.String,System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),
                ("M:System.Diagnostics.Process.Start(System.String,System.String,System.String,System.Security.SecureString,System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),
                ("M:System.Diagnostics.Process.Start(System.String,System.Collections.Generic.IEnumerable`1<System.String>)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),

                // MSB9998: ProcessStartInfo constructors - use TaskEnvironment.GetProcessStartInfo
                ("M:System.Diagnostics.ProcessStartInfo.#ctor", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),
                ("M:System.Diagnostics.ProcessStartInfo.#ctor(System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),
                ("M:System.Diagnostics.ProcessStartInfo.#ctor(System.String,System.String)", ApiCategory.TaskEnvironment, "Use TaskEnvironment.GetProcessStartInfo instead"),

                // MSB9999: Critical - ThreadPool (process-wide settings)
                ("M:System.Threading.ThreadPool.SetMinThreads(System.Int32,System.Int32)", ApiCategory.CriticalError, "Modifies process-wide thread pool settings"),
                ("M:System.Threading.ThreadPool.SetMaxThreads(System.Int32,System.Int32)", ApiCategory.CriticalError, "Modifies process-wide thread pool settings"),

                // MSB9999: Critical - CultureInfo (affects all new threads)
                ("P:System.Globalization.CultureInfo.DefaultThreadCurrentCulture", ApiCategory.CriticalError, "Affects all new threads in process"),
                ("P:System.Globalization.CultureInfo.DefaultThreadCurrentUICulture", ApiCategory.CriticalError, "Affects all new threads in process"),

                // MSB9996: Potential issues - Assembly loading
                ("M:System.Reflection.Assembly.LoadFrom(System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Reflection.Assembly.LoadFile(System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Reflection.Assembly.Load(System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Reflection.Assembly.Load(System.Byte[])", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Reflection.Assembly.Load(System.Byte[],System.Byte[])", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Reflection.Assembly.LoadWithPartialName(System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Activator.CreateInstanceFrom(System.String,System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.Activator.CreateInstance(System.String,System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),

                // MSB9996: Potential issues - Console I/O
                ("P:System.Console.Out", ApiCategory.PotentialIssue, "May interfere with build logging - use task logging methods instead"),
                ("P:System.Console.Error", ApiCategory.PotentialIssue, "May interfere with build logging - use task logging methods instead"),
                ("P:System.Console.In", ApiCategory.PotentialIssue, "May cause deadlocks in automated builds"),
                ("M:System.Console.Write(System.String)", ApiCategory.PotentialIssue, "May interfere with build output - use task logging methods instead"),
                ("M:System.Console.WriteLine(System.String)", ApiCategory.PotentialIssue, "May interfere with build output - use task logging methods instead"),
                ("M:System.Console.ReadLine", ApiCategory.PotentialIssue, "May cause deadlocks in automated builds"),
                ("M:System.Console.ReadKey", ApiCategory.PotentialIssue, "May cause deadlocks in automated builds"),

                // MSB9996: Potential issues - AppDomain
                ("M:System.AppDomain.Load(System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.AppDomain.Load(System.Byte[])", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.AppDomain.CreateInstanceFrom(System.String,System.String)", ApiCategory.PotentialIssue, "May cause version conflicts"),
                ("M:System.AppDomain.CreateInstance(System.String,System.String)", ApiCategory.PotentialIssue, "May cause version conflicts")
            );
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var bannedApis = BuildBannedApisDictionary(compilationContext.Compilation);
            if (bannedApis == null || bannedApis.Count == 0)
            {
                return;
            }

            // Register operation analysis
            compilationContext.RegisterOperationAction(
                context => AnalyzeOperationInContext(context, bannedApis),
                OperationKind.ObjectCreation,
                OperationKind.Invocation,
                OperationKind.EventReference,
                OperationKind.FieldReference,
                OperationKind.MethodReference,
                OperationKind.PropertyReference);
        }

        private Dictionary<string, BanFileEntry>? BuildBannedApisDictionary(Compilation compilation)
        {
            var result = new Dictionary<string, BanFileEntry>();
            var bannedApiDefinitions = GetBannedApiDefinitions();

            foreach (var (declarationId, category, message) in bannedApiDefinitions)
            {
                var symbols = GetSymbolsFromDeclarationId(compilation, declarationId);
                if (symbols.Any())
                {
                    result[declarationId] = new BanFileEntry(declarationId, category, message, symbols);
                }
            }

            return result.Count > 0 ? result : null;
        }

        private ImmutableArray<ISymbol> GetSymbolsFromDeclarationId(Compilation compilation, string declarationId)
        {
            // Simple implementation using DocumentationCommentId
            try
            {
                var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(declarationId, compilation);
                return symbols.ToArray().ToImmutableArray();
            }
            catch
            {
                // If parsing fails, return empty array
                return ImmutableArray<ISymbol>.Empty;
            }
        }

        private void AnalyzeOperationInContext(
            OperationAnalysisContext context,
            Dictionary<string, BanFileEntry> bannedApis)
        {
            // Check if we're in a class that implements IThreadSafeTask
            var containingType = GetContainingType(context.Operation);
            if (containingType == null || !IsIThreadSafeTaskImplementation(containingType))
            {
                return;
            }

            // Analyze the operation
            context.CancellationToken.ThrowIfCancellationRequested();

            switch (context.Operation)
            {
                case IInvocationOperation invocation:
                    // Check if this is a File/Directory method with unwrapped path argument
                    if (IsPathMethodWithUnwrappedArgument(invocation.TargetMethod, invocation.Arguments))
                    {
                        var diagnostic = Diagnostic.Create(
                            FilePathRequiresAbsoluteRule,
                            invocation.Syntax.GetLocation(),
                            invocation.TargetMethod.ToDisplayString(SymbolDisplayFormat),
                            ": Uses current working directory - use absolute paths with TaskEnvironment.GetAbsolutePath");
                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {
                        // Check if it's in the banned list
                        VerifySymbol(context.ReportDiagnostic, invocation.TargetMethod, context.Operation.Syntax, bannedApis);
                    }
                    break;

                case IMemberReferenceOperation memberReference:
                    VerifySymbol(context.ReportDiagnostic, memberReference.Member, context.Operation.Syntax, bannedApis);
                    break;

                case IObjectCreationOperation objectCreation:
                    if (objectCreation.Constructor != null)
                    {
                        // Check if this is a FileInfo/DirectoryInfo/FileStream/StreamReader/StreamWriter constructor with unwrapped path
                        if (IsPathMethodWithUnwrappedArgument(objectCreation.Constructor, objectCreation.Arguments))
                        {
                            var diagnostic = Diagnostic.Create(
                                FilePathRequiresAbsoluteRule,
                                objectCreation.Syntax.GetLocation(),
                                objectCreation.Constructor.ContainingType.ToDisplayString(SymbolDisplayFormat),
                                ": Constructor uses current working directory - use absolute paths with TaskEnvironment.GetAbsolutePath");
                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            // Check if it's in the banned list
                            VerifySymbol(context.ReportDiagnostic, objectCreation.Constructor, context.Operation.Syntax, bannedApis);
                        }
                    }
                    break;
            }
        }

        private INamedTypeSymbol? GetContainingType(IOperation operation)
        {
            var current = operation;
            while (current != null)
            {
                if (current.SemanticModel != null)
                {
                    var typeDeclaration = current.Syntax.Ancestors().FirstOrDefault(IsTypeDeclaration);
                    if (typeDeclaration != null)
                    {
                        var symbol = current.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                        if (symbol is INamedTypeSymbol typeSymbol)
                        {
                            return typeSymbol;
                        }
                    }
                }
                current = current.Parent;
            }
            return null;
        }

        private bool IsIThreadSafeTaskImplementation(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.AllInterfaces.Any(i => i.ToDisplayString() == IMultiThreadableTaskInterfaceName);
        }

        /// <summary>
        /// Checks if a method/constructor is on a path-related type with a string parameter
        /// that is NOT wrapped with TaskEnvironment.GetAbsolutePath().
        /// </summary>
        private bool IsPathMethodWithUnwrappedArgument(IMethodSymbol method, ImmutableArray<IArgumentOperation> arguments)
        {
            var containingTypeName = method.ContainingType?.ToDisplayString();
            if (containingTypeName == null)
            {
                return false;
            }

            // Check if this is one of the path-related types
            var pathTypes = GetTypesWithPathMethods();
            if (!pathTypes.Contains(containingTypeName))
            {
                return false;
            }

            // Check if method has at least one string parameter (likely a path)
            var hasStringParameter = method.Parameters.Any(p => p.Type.SpecialType == SpecialType.System_String);
            if (!hasStringParameter)
            {
                return false;
            }

            // Find the first string argument and check if it's wrapped
            foreach (var arg in arguments)
            {
                if (arg.Parameter?.Type.SpecialType == SpecialType.System_String)
                {
                    // Check if this argument is a call to TaskEnvironment.GetAbsolutePath()
                    if (IsWrappedWithGetAbsolutePath(arg.Value))
                    {
                        // Path is already wrapped - no warning needed
                        return false;
                    }
                    // Found unwrapped string argument - this needs a warning
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an operation is a call to TaskEnvironment.GetAbsolutePath().
        /// </summary>
        private bool IsWrappedWithGetAbsolutePath(IOperation operation)
        {
            // Check for direct call: TaskEnvironment.GetAbsolutePath(...)
            if (operation is IInvocationOperation invocation)
            {
                var methodName = invocation.TargetMethod.Name;
                var containingType = invocation.TargetMethod.ContainingType?.ToDisplayString();
                
                if (methodName == "GetAbsolutePath" && 
                    containingType == "Microsoft.Build.Framework.TaskEnvironment")
                {
                    return true;
                }
            }

            // Check for conversion from AbsolutePath to string
            if (operation is IConversionOperation conversion)
            {
                var sourceType = conversion.Operand.Type?.ToDisplayString();
                if (sourceType == "Microsoft.Build.Framework.AbsolutePath")
                {
                    return true;
                }
                
                // Recursively check the converted expression
                return IsWrappedWithGetAbsolutePath(conversion.Operand);
            }

            return false;
        }

        private void VerifySymbol(
            Action<Diagnostic> reportDiagnostic,
            ISymbol symbol,
            SyntaxNode syntaxNode,
            Dictionary<string, BanFileEntry> bannedApis)
        {
            foreach (var kvp in bannedApis)
            {
                var declarationId = kvp.Key;
                var entry = kvp.Value;
                if (entry.Symbols.Any(bannedSymbol => SymbolEqualityComparer.Default.Equals(symbol, bannedSymbol)))
                {
                    // Select the appropriate diagnostic rule based on category
                    var rule = entry.Category switch
                    {
                        ApiCategory.CriticalError => CriticalErrorRule,
                        ApiCategory.TaskEnvironment => TaskEnvironmentRequiredRule,
                        ApiCategory.PotentialIssue => PotentialIssueRule,
                        _ => TaskEnvironmentRequiredRule // Default fallback
                    };

                    var diagnostic = Diagnostic.Create(
                        rule,
                        syntaxNode.GetLocation(),
                        symbol.ToDisplayString(SymbolDisplayFormat),
                        string.IsNullOrWhiteSpace(entry.Message) ? "" : ": " + entry.Message);

                    reportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        private sealed class BanFileEntry
        {
            public string DeclarationId { get; }
            public ApiCategory Category { get; }
            public string Message { get; }
            public ImmutableArray<ISymbol> Symbols { get; }

            public BanFileEntry(string declarationId, ApiCategory category, string message, ImmutableArray<ISymbol> symbols)
            {
                DeclarationId = declarationId;
                Category = category;
                Message = message;
                Symbols = symbols;
            }
        }
    }
}
