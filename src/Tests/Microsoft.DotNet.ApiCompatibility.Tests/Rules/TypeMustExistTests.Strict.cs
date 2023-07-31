// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class TypeMustExistTests_Strict
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new MembersMustExist(settings, context));

        [Fact]
        public void MissingPublicTypesInLeftAreReported()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
  public class Second { }
  public struct MyStruct { }
#if !NETFRAMEWORK
  public record MyRecord(string a, string b);
#endif
}
";
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Second"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.MyStruct"),
#if !NETFRAMEWORK
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.MyRecord"),
#endif
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public void MissingTypeFromTypeForwardOnLeftIsReported()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string leftSyntax = @"
namespace CompatTests
{
  public class First { }
}
";
            string rightSyntax = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
namespace CompatTests
{
  public class First { }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntaxWithReferences(rightSyntax, new[] { forwardedTypeSyntax });
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.ForwardedTestType")
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public void TypeForwardExistsOnBothNoWarn()
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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            Assert.Empty(differ.GetDifferences(left, right));
        }

        [Fact]
        public void DifferenceIsIgnoredForMemberOnRight()
        {
            string leftSyntax = @"
namespace CompatTests
{
#if !NETFRAMEWORK
  public record First(string a, string b);
#endif
}
";
            string rightSyntax = @"
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Second"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Third"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Fourth"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.MyEnum")
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MissingNestedTypeOnLeftIsReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
  }
}
";
            string rightSyntax = @"

namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public class SecondNested { }
    }
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.First.FirstNested"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void TypesMissingOnBothSidesAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class LeftType { }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class RightType { }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.LeftType"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.RightType"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingTypesOnLeftAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First { }
}
";
            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public class First { }
}
",
            @"
namespace CompatTests
{
  public class First { }
  public class Second { }
}
",
            @"
namespace CompatTests
{
  public class First { }
  public class Third { }
}
"};

            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                new CompatDifference(left.MetadataInformation, right.ElementAt(1).MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Second"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(2).MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Third"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingNestedTypesOnLeftAreReported()
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
        public class ThirdNested
        {
          public string MyField;
        }
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
    public class FirstNested
    {
      public class SecondNested
      {
      }
    }
  }
}
"};
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                new CompatDifference(left.MetadataInformation, right.First().MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.First.FirstNested.SecondNested.ThirdNested"),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public void MultipleRightsMissingTypeForwardInLeftIsReported()
        {
            string forwardedTypeSyntax = @"
namespace CompatTests
{
  public class ForwardedTestType { }
}
";
            string leftSyntax = @"
namespace CompatTests
{
}";
            string rightWithForward = @"
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))]
";
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            string[] rightSyntaxes = new[] { rightWithForward, "namespace CompatTests { internal class Foo { } }", rightWithForward };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes, references);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                new CompatDifference(left.MetadataInformation, right.First().MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.ForwardedTestType"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(2).MetadataInformation, DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.ForwardedTestType"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
