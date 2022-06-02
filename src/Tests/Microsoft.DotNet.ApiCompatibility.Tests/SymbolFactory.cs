// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal static class SymbolFactory
    {
        internal static string EmitAssemblyFromSyntax(string syntax, bool enableNullable = false, byte[] publicKey = null, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey);

            Assert.Empty(compilation.GetDiagnostics());

            string assemblyDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString("D").Substring(0, 4)}-{assemblyName}");
            Directory.CreateDirectory(assemblyDir);
            string assemblyPath = Path.Combine(assemblyDir, $"{assemblyName}.dll");
            compilation.Emit(assemblyPath);

            return assemblyPath;
        }

        internal static IAssemblySymbol GetAssemblyFromSyntax(string syntax, bool enableNullable = false, byte[] publicKey = null, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey);

            Assert.Empty(compilation.GetDiagnostics());

            return compilation.Assembly;
        }

        internal static IAssemblySymbol GetAssemblyFromSyntaxWithReferences(string syntax, IEnumerable<string> referencesSyntax, bool enableNullable = false, byte[] publicKey = null, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, publicKey);
            CSharpCompilation compilationWithReferences = CreateCSharpCompilationFromSyntax(referencesSyntax, $"{assemblyName}_reference", enableNullable, publicKey);

            compilation = compilation.AddReferences(compilationWithReferences.ToMetadataReference());

            Assert.Empty(compilation.GetDiagnostics());

            return compilation.Assembly;
        }

        internal static IList<ElementContainer<IAssemblySymbol>> GetElementContainersFromSyntaxes(IEnumerable<string> syntaxes, IEnumerable<string> referencesSyntax = null, bool enableNullable = false, byte[] publicKey = null, [CallerMemberName] string assemblyName = "")
        {
            int i = 0;
            List<ElementContainer<IAssemblySymbol>> result = new();
            foreach (string syntax in syntaxes)
            {
                MetadataInformation info = new(string.Empty, string.Empty, $"runtime-{i++}");
                IAssemblySymbol symbol = referencesSyntax != null ?
                    GetAssemblyFromSyntaxWithReferences(syntax, referencesSyntax, enableNullable, publicKey, assemblyName) :
                    GetAssemblyFromSyntax(syntax, enableNullable, publicKey, assemblyName);

                ElementContainer<IAssemblySymbol> container = new(symbol, info);
                result.Add(container);
            }

            return result;
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(string syntax, string name, bool enableNullable, byte[] publicKey)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, publicKey);
            return compilation.AddSyntaxTrees(GetSyntaxTree(syntax));
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(IEnumerable<string> syntax, string name, bool enableNullable, byte[] publicKey)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, publicKey);
            IEnumerable<SyntaxTree> syntaxTrees = syntax.Select(s => GetSyntaxTree(s));
            return compilation.AddSyntaxTrees(syntaxTrees);
        }

        private static SyntaxTree GetSyntaxTree(string syntax)
        {
            return CSharpSyntaxTree.ParseText(syntax, ParseOptions);
        }

        private static CSharpCompilation CreateCSharpCompilation(string name, bool enableNullable, byte[] publicKey)
        {
            bool publicSign = publicKey != null ? true : false;
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                                  publicSign: publicSign,
                                                                  cryptoPublicKey: publicSign ? publicKey.ToImmutableArray() : default,
                                                                  nullableContextOptions: enableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
                                                                  specificDiagnosticOptions: DiagnosticOptions);

            return CSharpCompilation.Create(name, options: compilationOptions, references: DefaultReferences);
        }

        private static CSharpParseOptions ParseOptions { get; } = new(preprocessorSymbols:
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
        };
    }
}
