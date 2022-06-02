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
    public class CannotAddAbstractMemberTests
    {
        [Theory]
        [MemberData(nameof(AddedAbstractMemberIsReportedData))]
        public void AddedAbstractMemberIsReported(string leftSyntax, string rightSyntax, bool includeInternals)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternals;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotAddAbstractMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.SecondAbstract")
            };

            Assert.Equal(expected, differences);
        }

        [Theory]
        [MemberData(nameof(AddedAbstractMemberNoVisibleConstructorData))]
        public void AddedAbstractMemberNoVisibleConstructor(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Theory]
        [MemberData(nameof(AddedToUnsealedTypeInRightNotReportedData))]
        public void AddedToUnsealedTypeInRightNotReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
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

            ApiComparer differ = new();
            differ.StrictMode = true;
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

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expectedDiffs =
            {
                Array.Empty<CompatDifference>(),
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotAddAbstractMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.FirstNested.SecondNested.SomeAbstractMethod"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotAddAbstractMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.FirstNested.FirstNestedAbstract"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expectedDiffs, differences);
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
