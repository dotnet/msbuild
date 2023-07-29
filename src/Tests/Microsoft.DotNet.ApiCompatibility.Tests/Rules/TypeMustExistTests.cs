// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class TypeMustExistTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new MembersMustExist(settings, context));

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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Second"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyStruct"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyDelegate"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyEnum"),
#if !NETFRAMEWORK
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyRecord"),
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
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:A.B.C"),
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
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:A.B"),
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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.ForwardedTestType")
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
            ApiComparer differ = new(s_ruleFactory);

            Assert.Empty(differ.GetDifferences(left, right));
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Second"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Third"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Fourth"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.MyEnum")
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: includeInternalSymbols));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (!includeInternalSymbols)
            {
                Assert.Empty(differences);
            }
            else
            {
                CompatDifference[] expected =
                {
                    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.InternalType")
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: includeInternalSymbols));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            List<CompatDifference> expected = new()
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested"),
            };

            if (includeInternalSymbols)
            {
                expected.Add(
                  CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.InternalNested.DoubleNested")
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
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                    new CompatDifference(left.MetadataInformation, right[1].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First"),
                    new CompatDifference(left.MetadataInformation, right[2].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.Second"),
            };
            Assert.Equal(expected, differences);
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
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                new CompatDifference(left.MetadataInformation, right[0].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested.SecondNested.ThirdNested"),
                new CompatDifference(left.MetadataInformation, right[1].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested"),
                new CompatDifference(left.MetadataInformation, right[2].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First.FirstNested.SecondNested"),
                new CompatDifference(left.MetadataInformation, right[3].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.First"),
            };
            Assert.Equal(expected, differences);
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
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            string[] rightSyntaxes = new[] { leftSyntax, leftSyntax, leftSyntax, leftSyntax };
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
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
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(forwardedTypeSyntax),
                new MetadataInformation(string.Empty, "ref"));
            string[] rightSyntaxes = new[] { rightSyntax, rightSyntax, rightSyntax, rightSyntax, rightSyntax };
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes, references);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
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
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(forwardedTypeSyntax),
                new MetadataInformation(string.Empty, "ref"));
            string[] rightSyntaxes = new[] { rightWithForward, "namespace CompatTests { internal class Foo { } }", rightWithForward };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes, references);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                new CompatDifference(left.MetadataInformation, right[1].MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.ForwardedTestType"),
            };

            Assert.Equal(expected, differences);
        }
    }
}
