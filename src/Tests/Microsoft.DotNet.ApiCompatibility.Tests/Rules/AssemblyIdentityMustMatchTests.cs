// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class AssemblyIdentityMustMatchTests
    {
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
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Single(differences);

            CompatDifference expected = new(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, "AssemblyB, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
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

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Single(differences);

            CompatDifference expected = new(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{leftSymbol.Name}, Version=0.0.0.0, Culture=de, PublicKeyToken=null");
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

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);

            // right assembly should have same or higher version than left
            Assert.Single(differences);

            CompatDifference expected = new(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{rightSymbol.Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
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
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Empty(differences);

            differ.StrictMode = true;

            // Not strictly compatible
            differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Single(differences);

            CompatDifference expected = new(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{leftSymbol.Name}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
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
            ApiComparer differ = new();
            differ.StrictMode = strictMode;

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

            ApiComparer differ = new();
            differ.StrictMode = strictMode;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);

            if (strictMode)
            {
                Assert.Single(differences);
                CompatDifference expected = new(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{rightSymbol.Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
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

            ApiComparer differ = new();
            differ.StrictMode = strictMode;

            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftSymbol, rightSymbol);
            Assert.Single(differences);

            CompatDifference expected = new(DiagnosticIds.AssemblyIdentityMustMatch, string.Empty, DifferenceType.Changed, $"{leftSymbol.Name}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
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

            ApiComparer differ = new();
            differ.StrictMode = strictMode;

            Assert.Empty(differ.GetDifferences(leftSymbol, rightSymbol));
        }
    }
}

