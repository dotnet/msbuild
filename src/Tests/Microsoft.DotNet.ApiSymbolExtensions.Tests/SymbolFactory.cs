// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.ApiSymbolExtensions.Tests
{
    internal static class SymbolFactory
    {
        public static string EmitAssemblyFromSyntax(string syntax,
            bool enableNullable = false,
            byte[] publicKey = null,
            [CallerMemberName] string assemblyName = "",
            bool allowUnsafe = false)
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey, allowUnsafe);

            Assert.Empty(compilation.GetDiagnostics());

            string assemblyDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString("D").Substring(0, 4)}-{assemblyName}");
            Directory.CreateDirectory(assemblyDir);
            string assemblyPath = Path.Combine(assemblyDir, $"{assemblyName}.dll");
            compilation.Emit(assemblyPath);

            return assemblyPath;
        }

        public static Stream EmitAssemblyStreamFromSyntax(string syntax,
            bool enableNullable = false,
            byte[] publicKey = null,
            [CallerMemberName] string assemblyName = "",
            bool allowUnsafe = false)
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey, allowUnsafe);

            Assert.Empty(compilation.GetDiagnostics());

            MemoryStream stream = new();
            compilation.Emit(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static IAssemblySymbol GetAssemblyFromSyntax(string syntax,
            bool enableNullable = false,
            byte[] publicKey = null,
            [CallerMemberName] string assemblyName = "",
            bool allowUnsafe = false)
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey, allowUnsafe);

            Assert.Empty(compilation.GetDiagnostics());

            return compilation.Assembly;
        }

        public static IAssemblySymbol GetAssemblyFromSyntaxWithReferences(string syntax,
            IEnumerable<string> referencesSyntax,
            bool enableNullable = false,
            byte[] publicKey = null,
            [CallerMemberName] string assemblyName = "",
            bool allowUnsafe = false)
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey, allowUnsafe);
            CSharpCompilation compilationWithReferences = CreateCSharpCompilationFromSyntax(referencesSyntax, $"{assemblyName}_reference", enableNullable, publicKey, allowUnsafe);

            compilation = compilation.AddReferences(compilationWithReferences.ToMetadataReference());

            Assert.Empty(compilation.GetDiagnostics());

            return compilation.Assembly;
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(string syntax, string name, bool enableNullable, byte[] publicKey, bool allowUnsafe)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, publicKey, allowUnsafe);
            return compilation.AddSyntaxTrees(GetSyntaxTree(syntax));
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(IEnumerable<string> syntax, string name, bool enableNullable, byte[] publicKey, bool allowUnsafe)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, publicKey, allowUnsafe);
            IEnumerable<SyntaxTree> syntaxTrees = syntax.Select(s => GetSyntaxTree(s));
            return compilation.AddSyntaxTrees(syntaxTrees);
        }

        private static SyntaxTree GetSyntaxTree(string syntax)
        {
            return CSharpSyntaxTree.ParseText(syntax, ParseOptions);
        }

        private static CSharpCompilation CreateCSharpCompilation(string name, bool enableNullable, byte[] publicKey, bool allowUnsafe)
        {
            bool publicSign = publicKey != null ? true : false;
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                                  publicSign: publicSign,
                                                                  cryptoPublicKey: publicSign ? publicKey.ToImmutableArray() : default,
                                                                  nullableContextOptions: enableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
                                                                  allowUnsafe: allowUnsafe,
                                                                  specificDiagnosticOptions: DiagnosticOptions);

            return CSharpCompilation.Create(name, options: compilationOptions, references: DefaultReferences);
        }

        private static CSharpParseOptions ParseOptions { get; } = new CSharpParseOptions(preprocessorSymbols:
#if NETFRAMEWORK
                new string[] { "NETFRAMEWORK" }
#else
                Array.Empty<string>()
#endif
        );

        private static IEnumerable<KeyValuePair<string, ReportDiagnostic>> DiagnosticOptions { get; } = new[]
        {
            // Suppress warning for unused events.
            new KeyValuePair<string, ReportDiagnostic>("CS0067", ReportDiagnostic.Suppress)
        };

        private static IEnumerable<MetadataReference> DefaultReferences { get; } = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DynamicAttribute).Assembly.Location),
        };
    }
}
