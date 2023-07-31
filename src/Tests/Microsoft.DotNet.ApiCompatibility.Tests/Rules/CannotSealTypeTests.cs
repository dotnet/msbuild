// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotSealTypeTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotSealType(settings, context));

        [Theory]
        [MemberData(nameof(SealNonInheritableTypeNotReportedData))]
        public void SealNonInheritableTypeNotReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference difference = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First");
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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: includeInternals));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (!includeInternals)
            {
                Assert.Empty(differences);
            }
            else
            {
                CompatDifference[] expected = new[]
                {
                     CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
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
}
"};
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation("left", @"ref\a.dll"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            // Register CannotSealType and MemberMustExist rules as this test validates both.
            ApiComparer differ = new(s_ruleFactory.WithRule((settings, context) => new MembersMustExist(settings, context)));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expectedDiffs =
            {
                new CompatDifference(left.MetadataInformation, right.ElementAt(2).MetadataInformation, DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(3).MetadataInformation, DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(3).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.#ctor"),
            };
            Assert.Equal(expectedDiffs, differences);
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

            ElementContainer<IAssemblySymbol> leftContainer = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation("left", @"ref\a.dll"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftContainer, right);

            Assert.Empty(differences);
        }

        [Theory]
        [MemberData(nameof(StrictModeSealedLeftIsReportedData))]
        public void StrictModeSealedLeftIsReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            CompatDifference[] differences = differ.GetDifferences(left, right).ToArray();

            CompatDifference expectedDifference = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotSealType, string.Empty, DifferenceType.Changed, "T:CompatTests.First");
            Assert.Contains(expectedDifference, differences);
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
