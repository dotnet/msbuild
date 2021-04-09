using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal static class SymbolFactory
    {
        internal static IAssemblySymbol GetAssemblyFromSyntax(string syntax, bool enableNullable = false, bool includeDefaultReferences = false, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, includeDefaultReferences);
            return compilation.Assembly;
        }

        internal static IAssemblySymbol GetAssemblyFromSyntaxWithReferences(string syntax, IEnumerable<string> referencesSyntax, bool enableNullable = false, bool includeDefaultReferences = false, [CallerMemberName] string assemblyName = "")
        {
            CSharpCompilation compilation = CreateCSharpCompilationFromSyntax(syntax, assemblyName, enableNullable, includeDefaultReferences);
            CSharpCompilation compilationWithReferences = CreateCSharpCompilationFromSyntax(referencesSyntax, $"{assemblyName}_reference", enableNullable, includeDefaultReferences);

            compilation = compilation.AddReferences(compilationWithReferences.ToMetadataReference());
            return compilation.Assembly;
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(string syntax, string name, bool enableNullable, bool includeDefaultReferences)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, includeDefaultReferences);
            return compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(syntax));
        }

        private static CSharpCompilation CreateCSharpCompilationFromSyntax(IEnumerable<string> syntax, string name, bool enableNullable, bool includeDefaultReferences)
        {
            CSharpCompilation compilation = CreateCSharpCompilation(name, enableNullable, includeDefaultReferences);
            IEnumerable<SyntaxTree> syntaxTrees = syntax.Select(s => CSharpSyntaxTree.ParseText(s));
            return compilation.AddSyntaxTrees(syntaxTrees);
        }

        private static CSharpCompilation CreateCSharpCompilation(string name, bool enableNullable, bool includeDefaultReferences)
        {
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                                  nullableContextOptions: enableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable);

            return CSharpCompilation.Create(name, options: compilationOptions, references: includeDefaultReferences ? DefaultReferences : null);
        }

        private static IEnumerable<MetadataReference> DefaultReferences { get; } = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
    }
}
