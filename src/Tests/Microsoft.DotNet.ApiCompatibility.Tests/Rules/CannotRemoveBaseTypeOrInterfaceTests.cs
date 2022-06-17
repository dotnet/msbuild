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
    public class CannotRemoveBaseTypeOrInterfaceTests
    {
        [Fact]
        public void PromotedBaseClassOrInterfaceIsNotReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class Second : SecondBase { }
  public class SecondBase { }
  public class FirstBase { }
  public interface IFirstInterface { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First : NewBase { }
  public class Second : NewSecondBase { }
  public class NewBase : FirstBase, INewInterface { }
  public class FirstBase { }
  public class SecondBase { }
  public class NewSecondBase : NewSecondBaseBase { }
  public class NewSecondBaseBase : SecondBase { }
  public interface IFirstInterface { }
  public interface INewInterface : IFirstInterface { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();

            Assert.Empty(differ.GetDifferences(left, right));
        }

        [Fact]
        public void RemovedInterfaceAndBaseClassAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class FirstBase { }
  public interface IFirstInterface { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First : NewBase { }
  public class NewBase : INewInterface { }
  public class FirstBase { }
  public interface IFirstInterface { }
  public interface INewInterface { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();

            CompatDifference[] differences = differ.GetDifferences(left, right).ToArray();

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotRemoveBaseType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                new CompatDifference(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };

            Assert.Equal(expected, differences);

            differences[0].Message.Contains("CompatTests.FirstBase");
            differences[1].Message.Contains("CompatTests.IFirstInterface");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RemovedInternalInterfaceIsReportedWhenIncludeInternals(bool includeInternals)
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First : IFirstInternalInterface, IFirstInterface { }
  public interface IFirstInterface { }
  internal interface IFirstInternalInterface { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First : IFirstInterface { }
  public interface IFirstInterface { }
  internal interface IFirstInternalInterface { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternals;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (includeInternals)
            {
                CompatDifference[] expected = new[]
                {
                    new CompatDifference(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
                };

                Assert.Equal(expected, differences);
            }
            else
            {
                Assert.Empty(differences);
            }
        }

        [Fact]
        public void RemovedFromLeftReportedOnStrictMode()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First { }
  public class FirstBase { }
  public interface IFirstInterface { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class FirstBase { }
  public interface IFirstInterface { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.StrictMode = true;

            CompatDifference[] differences = differ.GetDifferences(left, right).ToArray();

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotRemoveBaseType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                new CompatDifference(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };

            Assert.Equal(expected, differences);

            string firstMessage = differences[0].Message;
            string secondMessage = differences[1].Message;
            firstMessage.Contains("CompatTests.FirstBase");
            secondMessage.Contains("CompatTests.IFirstInterface");

            Assert.True(firstMessage.IndexOf("right") > firstMessage.IndexOf("left"));
            Assert.True(secondMessage.IndexOf("right") > firstMessage.IndexOf("left"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SameOnBothSidesDoesNotFail(bool strictMode)
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class FirstBase : SecondBase { }
  public class SecondBase : ThirdBase, ISecondInterface { }
  public class ThirdBase : IThirdInterface { }
  public interface IFirstInterface : IFourthInterface { }
  public interface ISecondInterface { }
  public interface IThirdInterface { }
  public interface IFourthInterface { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class FirstBase : SecondBase { }
  public class SecondBase : ThirdBase, ISecondInterface { }
  public class ThirdBase : IThirdInterface { }
  public interface IFirstInterface : IFourthInterface { }
  public interface ISecondInterface { }
  public interface IThirdInterface { }
  public interface IFourthInterface { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.StrictMode = strictMode;

            Assert.Empty(differ.GetDifferences(left, right));
        }

        [Fact]
        public void MultiRightReportsRightDifferences()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class FirstBase : SecondBase { }
  public class SecondBase : ThirdBase, ISecondInterface { }
  public class ThirdBase { }
  public interface IFirstInterface { }
  internal interface ISecondInterface { }
}
";
            string[] rightSyntaxes = new[]
            {
                @"
namespace CompatTests
{
  public class First : FirstBase, IFirstInterface { }
  public class FirstBase : SecondBase { }
  public class SecondBase : ThirdBase, ISecondInterface { }
  public class ThirdBase { }
  public interface IFirstInterface { }
  internal interface ISecondInterface { }
}
",
                @"
namespace CompatTests
{
  public class First : FirstBase { }
  public class FirstBase : NewSecondBase { }
  public class NewSecondBase : SecondBase, IFirstInterface { }
  public class SecondBase : NewThirdBase, ISecondInterface { }
  public class NewThirdBase { }
  public class ThirdBase { }
  public interface IFirstInterface { }
  internal interface ISecondInterface { }
}
",
                @"
namespace CompatTests
{
  public class First : FirstBase { }
  public class FirstBase : NewSecondBase { }
  public class NewSecondBase : SecondBase { }
  public class SecondBase : NewThirdBase, ISecondInterface { }
  public class NewThirdBase : ThirdBase { }
  public class ThirdBase { }
  public interface IFirstInterface { }
  internal interface ISecondInterface { }
}
"
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
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotRemoveBaseType, string.Empty, DifferenceType.Changed, "T:CompatTests.SecondBase"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                },
            };

            AssertExtensions.MultiRightResult(leftMetadata, expectedDiffs, result);
        }

        [Fact]
        public void MembersPushedDownToNewBaseNotReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First : FirstBase { }
  public class Second : SecondBase
  {
    public string MyMethod() => string.Empty;
    public int MyProperty => 0;
  }
  public class SecondBase : ThirdBase
  {
    public void AnotherMethod() { }
    public string MethodWithArguments(string a) => MethodWithArguments(a, string.Empty);
    public string MethodWithArguments(string a, string b) => MethodWithArguments(a, string.Empty, string.Empty);
    public string MethodWithArguments(string a, string b, string c) => c;
  }
  public class FirstBase { }
  public class ThirdBase { }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First : NewBase { }
  public class Second : NewSecondBase
  {
    public int MyProperty => 0;
  }
  public class NewBase : FirstBase { }
  public class FirstBase { }
  public class NewSecondBase : SecondBase
  {
    public string MyMethod() => string.Empty;
  }
  public class SecondBase : NewThirdBase
  {
    public void AnotherMethod() { }
    public string MethodWithArguments(string a) => MethodWithArguments(a, string.Empty);
  }
  public class NewThirdBase : ThirdBase
  {
    public string MethodWithArguments(string a, string b) => MethodWithArguments(a, string.Empty, string.Empty);
    public string MethodWithArguments(string a, string b, string c) => c;
  }
  public class ThirdBase { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();

            Assert.Empty(differ.GetDifferences(left, right));
        }
    }
}
