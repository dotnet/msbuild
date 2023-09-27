// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class AssemblyIdentityMustMatchTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new AssemblyIdentityMustMatch(new SuppressibleTestLog(), settings, context));

        private static readonly byte[] _publicKey = new byte[]
        {
            0, 36, 0, 0, 4, 128, 0, 0, 148, 0, 0, 0, 6, 2, 0, 0, 0, 36, 0, 0,
            82, 83, 65, 49, 0, 4, 0, 0, 1, 0, 1, 0, 59, 95, 150, 159, 243, 67,
            213, 101, 13, 42, 127, 1, 28, 70, 32, 249, 95, 32, 222, 178, 241,
            112, 43, 130, 179, 253, 136, 12, 214, 69, 99, 48, 108, 1, 225, 85,
            43, 140, 249, 91, 96, 28, 32, 96, 222, 101, 30, 186, 118, 74, 97, 47,
            90, 203, 33, 109, 13, 224, 26, 68, 113, 252, 132, 189, 45, 113, 37, 194,
            246, 28, 250, 11, 142, 65, 158, 36, 69, 33, 123, 215, 206, 43, 179, 174,
            44, 66, 108, 152, 199, 61, 182, 176, 126, 115, 72, 67, 1, 234, 122, 214,
            208, 240, 99, 182, 103, 113, 54, 95, 253, 54, 249, 70, 150, 123, 230, 135,
            122, 189, 56, 195, 25, 62, 141, 151, 88, 234, 231, 156
        };

        [Fact]
        public static void AssemblyNamesDoNotMatch()
        {
            IAssemblySymbol left = CSharpCompilation.Create("AssemblyA").Assembly;
            IAssemblySymbol right = CSharpCompilation.Create("AssemblyB").Assembly;
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Single(differences);
            CompatDifference expected = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "AssemblyB, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First());
        }

        [Fact]
        public void AssemblyCultureMustBeCompatible()
        {
            string leftSyntax = "";
            string rightSyntax = "[assembly: System.Reflection.AssemblyCultureAttribute(\"de\")]";

            IAssemblySymbol leftSymbol = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol rightSymbol = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            Assert.Equal(string.Empty, leftSymbol.Identity.CultureName);
            Assert.Equal("de", rightSymbol.Identity.CultureName);

            ApiComparer differ = new(s_ruleFactory);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);

            Assert.Single(differences);
            CompatDifference expected = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{leftSymbol.Name}, Version=0.0.0.0, Culture=de, PublicKeyToken=null");
            Assert.Equal(expected, differences.First());
        }

        [Fact]
        public void AssemblyVersionMustBeCompatible()
        {
            string leftSyntax = "[assembly: System.Reflection.AssemblyVersionAttribute(\"2.0.0.0\")]";
            string rightSyntax = "namespace EmptyNS { }";

            IAssemblySymbol leftSymbol = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol rightSymbol = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            Assert.Equal(new Version(2, 0, 0, 0), leftSymbol.Identity.Version);
            Assert.Equal(new Version(0, 0, 0, 0), rightSymbol.Identity.Version);

            ApiComparer differ = new(s_ruleFactory);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);

            // right assembly should have same or higher version than left
            Assert.Single(differences);
            CompatDifference expected = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{rightSymbol.Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First());
        }

        [Fact]
        public void AssemblyVersionMustBeStrictlyCompatible()
        {
            string leftSyntax = "[assembly: System.Reflection.AssemblyVersionAttribute(\"1.0.0.0\")]";
            string rightSyntax = "[assembly: System.Reflection.AssemblyVersionAttribute(\"2.0.0.0\")]";

            IAssemblySymbol leftSymbol = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol rightSymbol = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            Assert.Equal(new Version(1, 0, 0, 0), leftSymbol.Identity.Version);
            Assert.Equal(new Version(2, 0, 0, 0), rightSymbol.Identity.Version);

            // Compatible assembly versions
            ApiComparer differ = new(s_ruleFactory);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Empty(differences);

            differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            // Not strictly compatible
            differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Single(differences);

            CompatDifference expected = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{leftSymbol.Name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AssemblyKeyTokenMustBeCompatible(bool strictMode)
        {
            string syntax = "namespace EmptyNs { }";

            IAssemblySymbol leftSymbol = SymbolFactory.GetAssemblyFromSyntax(syntax, publicKey: _publicKey);
            IAssemblySymbol rightSymbol = SymbolFactory.GetAssemblyFromSyntax(syntax, publicKey: _publicKey);

            Assert.Equal(_publicKey, leftSymbol.Identity.PublicKey);
            Assert.Equal(_publicKey, rightSymbol.Identity.PublicKey);

            // public key tokens must match
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: strictMode));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Empty(differences);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LeftAssemblyKeyTokenNull(bool strictMode)
        {
            string syntax = "namespace EmptyNs { }";

            IAssemblySymbol leftSymbol = SymbolFactory.GetAssemblyFromSyntax(syntax);
            IAssemblySymbol rightSymbol = SymbolFactory.GetAssemblyFromSyntax(syntax, publicKey: _publicKey);

            Assert.False(leftSymbol.Identity.HasPublicKey);
            Assert.Equal(_publicKey, rightSymbol.Identity.PublicKey);

            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: strictMode));
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);

            if (strictMode)
            {
                Assert.Single(differences);
                CompatDifference expected = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{rightSymbol.Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                Assert.Equal(expected, differences.First());
            }
            else
            {
                Assert.Empty(differences);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RightAssemblyKeyTokenNull(bool strictMode)
        {
            string syntax = "namespace EmptyNs { }";

            IAssemblySymbol leftSymbol = SymbolFactory.GetAssemblyFromSyntax(syntax, publicKey: _publicKey);
            IAssemblySymbol rightSymbol = SymbolFactory.GetAssemblyFromSyntax(syntax);

            Assert.Equal(_publicKey, leftSymbol.Identity.PublicKey);
            Assert.False(rightSymbol.Identity.HasPublicKey);

            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: strictMode));
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);

            Assert.Single(differences);
            CompatDifference expected = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{leftSymbol.Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Assert.Equal(expected, differences.First());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RetargetableFlagSet(bool strictMode)
        {
            string syntax = @"
using System.Reflection;

[assembly: AssemblyFlags(AssemblyNameFlags.Retargetable)]
";

            // Emitting the assembly to a physical location to workaround:
            // https://github.com/dotnet/roslyn/issues/54836
            string leftAssembly = SymbolFactory.EmitAssemblyFromSyntax(syntax, publicKey: _publicKey);
            string rightAssembly = SymbolFactory.EmitAssemblyFromSyntax(syntax);

            IAssemblySymbol leftSymbol = new AssemblySymbolLoader().LoadAssembly(leftAssembly);
            IAssemblySymbol rightSymbol = new AssemblySymbolLoader().LoadAssembly(rightAssembly);

            Assert.True(leftSymbol.Identity.IsRetargetable);
            Assert.True(rightSymbol.Identity.IsRetargetable);
            Assert.False(rightSymbol.Identity.HasPublicKey);
            Assert.Equal(_publicKey, leftSymbol.Identity.PublicKey);

            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: strictMode));

            Assert.Empty(differ.GetDifferences(leftSymbol, rightSymbol));
        }
    }
}

