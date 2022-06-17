// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotSealTypeTests
    {
        [Theory]
        [MemberData(nameof(SealNonInheritableTypeNotReportedData))]
        public void SealNonInheritableTypeNotReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            foreach (CompatDifference difference in differences)
            {
                Assert.NotEqual(DiagnosticIds.CannotSealType, difference.DiagnosticId);
            }
        }

        [Theory]
        [MemberData(nameof(SealInheritableTypeReportedData))]
        public void SealInheritableTypeReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference difference = new(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First");

            Assert.Contains(difference, differences);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SealInheritableTypeInternalsVisibleReported(bool includeInternals)
        {
            string leftSyntax = @"
namespace CompatTests
{
    public class First
    {
        internal First() { }
    }
}
";
            string rightSyntax = @"
namespace CompatTests
{
    public sealed class First
    {
        internal First() { }
    }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternals;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (!includeInternals)
            {
                Assert.Empty(differences);
            }
            else
            {
                CompatDifference[] expected = new[]
                {
                     new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
                };

                Assert.Equal(expected, differences);
            }

        }

        [Fact]
        public void MultipleRightsAreReportedCorrectly()
        {
            string leftSyntax = @"
namespace CompatTests
{
    public class First
    {
    }
}
";

            string[] rightSyntaxes = new[]
            {
                @"
namespace CompatTests
{
    public class First
    {
    }
}",
                @"
namespace CompatTests
{
    public class First
    {
        protected First() { }
    }
}",
                @"
namespace CompatTests
{
    public sealed class First
    {
    }
}",
                @"
namespace CompatTests
{
    public class First
    {
        private First() { }
    }
}",
            };

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            MetadataInformation leftMetadata = new("left", "net6.0", "ref/a.dll");
            ElementContainer<IAssemblySymbol> leftContainer = new(left, leftMetadata);

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            ApiComparer differ = new();
            IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> result =
                differ.GetDifferences(leftContainer, right);

            CompatDifference[][] expectedDiffs =
            {
                Array.Empty<CompatDifference>(),
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.#ctor"),
                },
            };

            AssertExtensions.MultiRightResult(leftMetadata, expectedDiffs, result);
        }

        [Fact]
        public void MultipleRightsNoDifferences()
        {
            string leftSyntax = @"
namespace CompatTests
{
    public class First
    {
    }
}
";

            string[] rightSyntaxes = new[]
            {
                @"
namespace CompatTests
{
    public class First
    {
    }
}",
                @"
namespace CompatTests
{
    public class First
    {
        protected First() { }
    }
}",
                @"
namespace CompatTests
{
    public class First
    {
        internal First() { }
    }
}"
            };

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            MetadataInformation leftMetadata = new("left", "net6.0", "ref/a.dll");
            ElementContainer<IAssemblySymbol> leftContainer = new(left, leftMetadata);

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = true;
            IEnumerable<(MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences)> result =
                differ.GetDifferences(leftContainer, right);

            AssertExtensions.MultiRightEmptyDifferences(leftMetadata, 3, result);
        }

        [Theory]
        [MemberData(nameof(StrictModeSealedLeftIsReportedData))]
        public void StrictModeSealedLeftIsReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.StrictMode = true;
            CompatDifference[] differences = differ.GetDifferences(left, right).ToArray();

            CompatDifference difference = new(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First");

            Assert.Contains(difference, differences);
            Assert.True(differences[0].Message.IndexOf("left") < differences[0].Message.IndexOf("right"));
        }

        public static IEnumerable<object[]> StrictModeSealedLeftIsReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
    public sealed class First
    {
        public First() { }
    }
}
",
                @"
namespace CompatTests
{
    public class First
    {
      public First() { }
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public class First
    {
        private First() { }
    }
}
",
                @"
namespace CompatTests
{
    public class First
    {
        public First() { }
    }
}
"
            };
        }

        public static IEnumerable<object[]> SealInheritableTypeReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
    public class First
    {
    }
}
",
                @"
namespace CompatTests
{
    public sealed class First
    {
      public First() { }
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public class First
    {
        public First() { }
    }
}
",
                @"
namespace CompatTests
{
    public class First
    {
        private First() { }
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public class First
    {
        public First() { }
    }
}
",
                @"
namespace CompatTests
{
    public class First
    {
        private First() { }
        static First() { }
    }
}
"
            };
        }

        public static IEnumerable<object[]> SealNonInheritableTypeNotReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
    public class SealedClass
    {
        private SealedClass() { } 
    }
}
",
                @"
namespace CompatTests
{
    public sealed class SealedClass
    {
        public SealedClass() { } 
    }
}
"
            };

            yield return new[]
            {
                @"
namespace CompatTests
{
    public sealed class SealedClass
    {
    }
}
",
                @"
namespace CompatTests
{
    public class SealedClass
    {
        private SealedClass() { } 
    }
}
"
            };
        }
    }
}
