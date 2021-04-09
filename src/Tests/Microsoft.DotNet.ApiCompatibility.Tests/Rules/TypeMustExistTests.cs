using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class TypeMustExistTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("CP002")]
        public void MissingPublicTypesInRightAreReported(string noWarn)
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
  public record MyRecord(string a, string b);
  public struct MyStruct { }
  public delegate void MyDelegate(object a);
  public enum MyEnum { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiDiffer differ = new();
            differ.NoWarn = noWarn;
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Second' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyRecord' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.MyRecord"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyStruct' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.MyStruct"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyDelegate' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.MyDelegate"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyEnum' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.MyEnum"),
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(leftSyntax, new[] { forwardedTypeSyntax }, includeDefaultReferences: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiDiffer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.ForwardedTestType' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.ForwardedTestType")
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references, includeDefaultReferences: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references, includeDefaultReferences: true);
            ApiDiffer differ = new();
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void NoDifferencesReportedWithNoWarn()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public class First { }
  public class Second { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First { }
}
";

            ApiDiffer differ = new();
            differ.NoWarn = DiagnosticIds.TypeMustExist;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void DifferenceIsIgnoredForMember()
        {
            string leftSyntax = @"

namespace CompatTests
{
  public record First(string a, string b);
  public class Second { }
  public class Third { }
  public class Fourth { }
  public enum MyEnum { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public record First(string a, string b);
}
";

            (string, string)[] ignoredDifferences = new[]
            {
                (DiagnosticIds.TypeMustExist, "T:CompatTests.Second"),
                (DiagnosticIds.TypeMustExist, "T:CompatTests.MyEnum"),
            };

            ApiDiffer differ = new();
            differ.IgnoredDifferences = ignoredDifferences;

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Third' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.Third"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Fourth' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.Fourth")
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

            ApiDiffer differ = new(includeInternalSymbols);
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
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.InternalType' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.InternalType")
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

            ApiDiffer differ = new(includeInternalSymbols);
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            List<CompatDifference> expected = new()
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.First.FirstNested"),
            };

            if (includeInternalSymbols)
            {
                expected.Add(
                  new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.InternalNested.DoubleNested' exists on the contract but not on the implementation", DifferenceType.Removed, "T:CompatTests.First.InternalNested.DoubleNested")
                );
            }

            Assert.Equal(expected, differences);
        }
    }
}
