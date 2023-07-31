// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotAddAbstractMemberTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotAddAbstractMember(settings, context));

        [Theory]
        [MemberData(nameof(AddedAbstractMemberIsReportedData))]
        public void AddedAbstractMemberIsReported(string leftSyntax, string rightSyntax, bool includeInternals)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: includeInternals));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAbstractMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.SecondAbstract")
            };
            Assert.Equal(expected, differences);
        }

        [Theory]
        [MemberData(nameof(AddedAbstractMemberNoVisibleConstructorData))]
        public void AddedAbstractMemberNoVisibleConstructor(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Theory]
        [MemberData(nameof(AddedToUnsealedTypeInRightNotReportedData))]
        public void AddedToUnsealedTypeInRightNotReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Fact]
        public void StrictModeRuleIsNotExecuted()
        {
            object[] syntaxes = AddedAbstractMemberIsReportedData().First();
            string leftSyntax = syntaxes[0] as string;
            string rightSyntax = syntaxes[1] as string;

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            foreach (CompatDifference difference in differences)
            {
                Assert.NotEqual(DiagnosticIds.CannotAddAbstractMember, difference.DiagnosticId);
            }
        }

        [Fact]
        public void MultipleRightsAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public abstract class First
  {
    public abstract class FirstNested
    {
      public class SecondNested
      {
        public void SomeMethod() { }
      }
    }
  }
}
";
            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public abstract class First
  {
    public abstract class FirstNested
    {
      public class SecondNested
      {
        public void SomeMethod() { }
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public abstract class First
  {
    public abstract class FirstNested
    {
      public abstract class SecondNested
      {
        public void SomeMethod() { }
        public abstract void SomeAbstractMethod();
      }
    }
  }
}
",
            @"
namespace CompatTests
{
  public abstract class First
  {
    public abstract class FirstNested
    {
      public abstract void FirstNestedAbstract();
      public class SecondNested
      {
        public void SomeMethod() { }
      }
    }
  }
}
"};
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expectedDiffs =
            {
                new CompatDifference(left.MetadataInformation, right[1].MetadataInformation, DiagnosticIds.CannotAddAbstractMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.FirstNested.SecondNested.SomeAbstractMethod"),
                new CompatDifference(left.MetadataInformation, right[2].MetadataInformation, DiagnosticIds.CannotAddAbstractMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.FirstNested.FirstNestedAbstract"),
            };
            Assert.Equal(expectedDiffs, differences);
        }

        public static IEnumerable<object[]> AddedToUnsealedTypeInRightNotReportedData()
        {
            yield return new object[]
            {
                @"
namespace CompatTests
{
  public sealed class First
  {
    public void SomeMethod() { }
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    public void SomeMethod() { }
    public abstract void SomeAbstractMember();
  }
}
"
            };

            yield return new object[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    private First() { }
    public void SomeMethod() { }
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    public First() { }
    public void SomeMethod() { }
    public abstract void SomeAbstractMember();
  }
}
"
            };

            yield return new object[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    internal First() { }
    public void SomeMethod() { }
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    protected First() { }
    public void SomeMethod() { }
    public abstract void SomeAbstractMember();
  }
}
"
            };
        }

        public static IEnumerable<object[]> AddedAbstractMemberIsReportedData()
        {
            yield return new object[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    public abstract void FirstAbstract();
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    public abstract void FirstAbstract();
    public abstract string SecondAbstract();
  }
}
",
                false
            };
            yield return new object[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    protected First() { }
    public abstract void FirstAbstract();
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    protected First() { }
    public abstract void FirstAbstract();
    public abstract string SecondAbstract();
  }
}
",
                false
            };
            yield return new object[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    internal First() { }
    public abstract void FirstAbstract();
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    internal First() { }
    public abstract void FirstAbstract();
    public abstract string SecondAbstract();
  }
}
",
                true
            };
        }
        public static IEnumerable<object[]> AddedAbstractMemberNoVisibleConstructorData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    private First() { }
    public abstract void FirstAbstract();
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    private First() { }
    public abstract void FirstAbstract();
    public abstract string SecondAbstract();
  }
}
"

            };

            yield return new object[]
            {
                @"
namespace CompatTests
{
  public abstract class First
  {
    internal First() { }
    public abstract void FirstAbstract();
  }
}
",
                @"
namespace CompatTests
{
  public abstract class First
  {
    internal First() { }
    public abstract void FirstAbstract();
    public abstract string SecondAbstract();
  }
}
"
            };
        }

    }
}
