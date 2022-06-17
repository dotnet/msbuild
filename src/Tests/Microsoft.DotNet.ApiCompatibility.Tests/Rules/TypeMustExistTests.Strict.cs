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
    public class TypeMustExistTests_Strict
    {
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

            ApiComparer differ = new();
            differ.StrictMode = true;
            bool enableNullable = false;
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, enableNullable);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.MyStruct"),
#if !NETFRAMEWORK
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.MyRecord"),
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
            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.ForwardedTestType")
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
            ApiComparer differ = new();
            differ.StrictMode = true;
            Assert.Empty(differ.GetDifferences(new[] { left }, new[] { right }));
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

            ApiComparer differ = new();
            differ.StrictMode = true;

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Second"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Third"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Fourth"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.MyEnum")
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
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.First.FirstNested"),
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
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:CompatTests.LeftType"),
                new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.RightType"),
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

            CompatDifference[][] expected =
            {
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Second"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.Third"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expected, differences);
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

            CompatDifference[][] expected =
            {
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.First.FirstNested.SecondNested.ThirdNested"),
                },
                Array.Empty<CompatDifference>(),
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expected, differences);
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
            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes, references);

            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expected =
            {
                new []
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.ForwardedTestType"),
                },
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:CompatTests.ForwardedTestType"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expected, differences);
        }
    }
}
