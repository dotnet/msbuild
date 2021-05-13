// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class TypeMustExistTests_Strict
    {
        [Theory]
        [InlineData("")]
        [InlineData("CP002")]
        public void MissingPublicTypesInLeftAreReported(string noWarn)
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
  public record MyRecord(string a, string b);
  public struct MyStruct { }
}
";

            ApiComparer differ = new();
            differ.NoWarn = noWarn;
            differ.StrictMode = true;
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Second' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyRecord' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.MyRecord"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.MyStruct' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.MyStruct"),
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
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(CompatTests.ForwardedTestType))];
namespace CompatTests
{
  public class First { }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntaxWithReferences(rightSyntax, new[] { forwardedTypeSyntax }, includeDefaultReferences: true);
            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.ForwardedTestType' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.ForwardedTestType")
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references, includeDefaultReferences: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, references, includeDefaultReferences: true);
            ApiComparer differ = new();
            differ.StrictMode = true;
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void NoDifferencesReportedWithNoWarn()
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
}
";

            ApiComparer differ = new();
            differ.StrictMode = true;
            differ.NoWarn = DiagnosticIds.TypeMustExist;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
        }

        [Fact]
        public void DifferenceIsIgnoredForMemberOnRight()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public record First(string a, string b);
}
";

            string rightSyntax = @"

namespace CompatTests
{
  public record First(string a, string b);
  public class Second { }
  public class Third { }
  public class Fourth { }
  public enum MyEnum { }
}
";

            (string, string)[] ignoredDifferences = new[]
            {
                (DiagnosticIds.TypeMustExist, "T:CompatTests.Second"),
                (DiagnosticIds.TypeMustExist, "T:CompatTests.MyEnum"),
            };

            ApiComparer differ = new();
            differ.IgnoredDifferences = ignoredDifferences;
            differ.StrictMode = true;

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Third' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.Third"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Fourth' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.Fourth")
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

            ApiComparer differ = new();
            differ.StrictMode = true;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            List<CompatDifference> expected = new()
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.First.FirstNested"),
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
            ApiComparer differ = new();
            differ.StrictMode = true;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            List<CompatDifference> expected = new()
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.LeftType' exists on the left but not on the right", DifferenceType.Removed, "T:CompatTests.LeftType"),
                new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.RightType' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.RightType"),
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

            ApiComparer differ = new();
            differ.StrictMode = true;
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), Array.Empty<CompatDifference>()),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Second' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.Second"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-2"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.Third' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.Third"),
                }),
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

            ApiComparer differ = new();
            differ.StrictMode = true;
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.First.FirstNested.SecondNested.ThirdNested' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.First.FirstNested.SecondNested.ThirdNested"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), Array.Empty<CompatDifference>()),
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
            string[] rightSyntaxes = new[] { rightWithForward, "namespace CompatTests { internal class Foo { } }", rightWithForward };
            IEnumerable<string> references = new[] { forwardedTypeSyntax };
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));
            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes, references, includeDefaultReferences: true);

            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.ForwardedTestType' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.ForwardedTestType"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), Array.Empty<CompatDifference>()),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-2"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, $"Type 'CompatTests.ForwardedTestType' exists on the right but not on the left", DifferenceType.Added, "T:CompatTests.ForwardedTestType"),
                }),
            };

            Assert.Equal(expected, differences);
        }
    }
}
