// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotRemoveBaseTypeOrInterfaceTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotRemoveBaseTypeOrInterface(settings, context));

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

            ApiComparer differ = new(s_ruleFactory);

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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveBaseType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };
            Assert.Equal(expected, differences);

            Assert.Contains("CompatTests.FirstBase", differences.ElementAt(0).Message);
            Assert.Contains("CompatTests.IFirstInterface", differences.ElementAt(1).Message);
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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: includeInternals));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (includeInternals)
            {
                CompatDifference[] expected = new[]
                {
                    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            CompatDifference[] differences = differ.GetDifferences(left, right).ToArray();

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveBaseType, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };
            Assert.Equal(expected, differences);

            string firstMessage = differences[0].Message;
            string secondMessage = differences[1].Message;

            Assert.Contains("CompatTests.FirstBase", firstMessage);
            Assert.Contains("CompatTests.IFirstInterface", secondMessage);

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

            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: strictMode));

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
            ElementContainer<IAssemblySymbol> leftContainer = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation("left", @"ref\a.dll"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(leftContainer, right);

            CompatDifference[] expectedDiffs =
            {
                new CompatDifference(leftContainer.MetadataInformation, right[1].MetadataInformation, DiagnosticIds.CannotRemoveBaseType, string.Empty, DifferenceType.Changed, "T:CompatTests.SecondBase"),
                new CompatDifference(leftContainer.MetadataInformation, right[2].MetadataInformation, DiagnosticIds.CannotRemoveBaseInterface, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };

            Assert.Equal(expectedDiffs, differences);
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
            ApiComparer differ = new(s_ruleFactory);

            Assert.Empty(differ.GetDifferences(left, right));
        }
    }
}
