// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class TypeMustExistTests
    {
        [Fact]
        public void MissingPublicTypesInRightAreReported()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
  public struct MyStruct { }
  public delegate void MyDelegate(object a);
  public enum MyEnum { }
#if !NETFRAMEWORK
  public record MyRecord(string a, string b);
#endif
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiComparer differ = new();
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyStruct"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyDelegate"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyEnum"),
#if !NETFRAMEWORK
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyRecord"),
#endif
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void TypeInDifferentNamespaces()
        {

            string leftSyntax = @"
namespace A.B
{
  public class C { }
}
";
            string rightSyntax = @"
namespace B.B
{
  public class C { }
}
";
            ApiComparer differ = new();
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left , right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:A.B.C"),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void NestedTypeVsNamespaces()
        {

            string leftSyntax = @"
namespace A
{
  public class B { }
}
";
            string rightSyntax = @"
public class A
{
  public class B { }
}
";
            ApiComparer differ = new();
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:A.B"),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void MissingTypeFromTypeForwardIsReported()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string leftSyntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
namespace CompatTests
{
  public class First { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(leftSyntax, new[] { forwardedTypeSyntax });
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.ForwardedTestType")
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void TypeForwardExistsOnBoth()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string syntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
namespace CompatTests
{
  public class First { }
}
";
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references);
            ApiComparer differ = new();
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void RecordsAreMappedCorrectly()
        {
            string leftSyntax = @"

namespace CompatTests
{
#if !NETFRAMEWORK
  public record First(string a, string b);
#endif
  public class Second { }
  public class Third { }
  public class Fourth { }
  public enum MyEnum { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
#if !NETFRAMEWORK
  public record First(string a, string b);
#endif
}
";

            ApiComparer differ = new();
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Third"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Fourth"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyEnum")
            };

            Assert.Equal(expected, differences);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InternalTypesAreIgnoredWhenSpecified(bool includeInternalSymbols)
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  internal class InternalType { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternalSymbols;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            if (!includeInternalSymbols)
            {
                Assert.Empty(differences);
            }
            else
            {
                CompatDifference[] expected = new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.InternalType")
                };

                Assert.Equal(expected, differences);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void MissingNestedTypeIsReported(bool includeInternalSymbols)
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested { }
    }
    internal class InternalNested
    {
        internal class DoubleNested { }
    }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    internal class InternalNested { }
  }
}
";

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternalSymbols;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            List<CompatDifference> expected = new()
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested"),
            };

            if (includeInternalSymbols)
            {
                expected.Add(
                  new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.InternalNested.DoubleNested")
                );
            }

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingTypesReported()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First { }
  public class Second { }
}
",
            @"
namespace CompatTests
{
  public class Second { }
}
",
            @"
namespace CompatTests
{
  public class First { }
}
"};

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expected =
            {
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Second"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingNestedTypesAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
      }
    }
  }
}
",
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
  public class First
  {
    public class FirstNested
    {
    }
  }
}
",
            @"
namespace CompatTests
{
}
"};

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expected =
            {
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested.SecondNested.ThirdNested"),
                },
                new[] {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested")
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested.SecondNested"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expected, differences);
        }

        [Fact]
        public static void MultipleRightsNoDifferences()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested
      {
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[] { leftSyntax, leftSyntax, leftSyntax, leftSyntax };

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            AssertExtensions.MultiRightEmptyDifferences(left.MetadataInformation, rightSyntaxes.Length, differences);
        }

        [Fact]
        public void MultipleRightsTypeForwardExistsOnAll()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string rightSyntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
";
            string[] rightSyntaxes = new[] { rightSyntax, rightSyntax, rightSyntax, rightSyntax, rightSyntax };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(forwardedTypeSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));
            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes, references);

            ApiComparer differ = new();
            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            AssertExtensions.MultiRightEmptyDifferences(left.MetadataInformation, rightSyntaxes.Length, differences);
        }

        [Fact]
        public void MultipleRightsMissingTypeForwardIsReported()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string rightWithForward = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
";
            string[] rightSyntaxes = new[] { rightWithForward, "namespace CompatTests { internal class Foo { } }", rightWithForward };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(forwardedTypeSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));
            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes, references);

            ApiComparer differ = new();
            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expected =
            {
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.ForwardedTestType"),
                },
                Array.Empty<CompatDifference>(),
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expected, differences);
        }
    }
}
