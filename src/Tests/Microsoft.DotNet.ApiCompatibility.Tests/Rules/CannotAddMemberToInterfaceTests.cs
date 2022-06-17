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
    public class CannotAddMemberToInterfaceTests
    {
        [Fact]
        public void AddedMembersAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public interface IFoo
  {
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    byte MyPropertyWithoutDefaultImplementation { get; set; }
    event System.EventHandler MyEventWithoutImplementation;

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    static int MyField = 2;
    event System.EventHandler MyEvent { add { } remove { } }
    int MyPropertyWithDefaultImplementation { get => 0; set { } }
#endif
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "M:CompatTests.IFoo.MyMethod"),
                new CompatDifference(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "P:CompatTests.IFoo.MyPropertyWithoutDefaultImplementation"),
                new CompatDifference(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "E:CompatTests.IFoo.MyEventWithoutImplementation"),
            };

            Assert.Equal(expected, differences);
        }

        [Theory]
        [MemberData(nameof(NoDifferencesShouldBeReportedData))]
        public void NoDifferencesShouldBeReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Fact]
        public void StrictModeRuleShouldNotRun()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public interface IFoo
  {
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    byte MyPropertyWithoutDefaultImplementation { get; set; }
    event System.EventHandler MyEventWithoutImplementation;

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    static int MyField = 2;
    int MyPropertyWithDefaultImplementation { get => 0; set { } }
    event System.EventHandler MyEvent { add { } remove { } }
#endif
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            differ.StrictMode = true;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            foreach (CompatDifference difference in differences)
            {
                Assert.NotEqual(DiagnosticIds.CannotAddMemberToInterface, difference.DiagnosticId);
            }
        }

        [Fact]
        public void MultipleRightsAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    int MyProperty { get; set; }
    event System.EventHandler MyEvent;
  }
}
";

            string[] rightSyntaxes = new[]
            { @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    int MyProperty { get; set; }
    event System.EventHandler MyEvent;

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    int MyPropertyWithDIM { get => 0; set { } }
#endif
  }
}
",
            @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    int MyProperty { get; set; }
    event System.EventHandler MyEvent;
    event System.EventHandler MyOtherEvent;

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    static int MyField = 3;
#endif
  }
}
",
            @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    string MyOtherMethod();
    int MyProperty { get; set; }
    event System.EventHandler MyEvent;

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    string MyOtherMethodWithDIM() => string.Empty;
    event System.EventHandler MyOtherEventWithDIM { add { } remove { } }
    static int MyField = 3;
#endif
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
                    new CompatDifference(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "E:CompatTests.IFoo.MyOtherEvent"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "M:CompatTests.IFoo.MyOtherMethod"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expectedDiffs, differences);
        }

        public static IEnumerable<object[]> NoDifferencesShouldBeReportedData()
        {
            yield return new[]
            {
                @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
  }
}
",
                @"
namespace CompatTests
{
  public interface IFoo
  {
    string MyMethod();
    internal string MyPrivateMethod();
  }
}
"
            };
            yield return new[]
            {
                @"
namespace CompatTests
{
  public interface IFoo
  {
    int MyProperty { get; set; }
  }
}
",
                @"
namespace CompatTests
{
  public interface IFoo
  {
    int MyProperty { get; set; }

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    byte MyOtherProperty { get => (byte)0; set { } }
#endif
  }
}
"
            };
            yield return new[]
            {
                @"
namespace CompatTests
{
  public interface IFoo
  {
    event System.EventHandler MyEvent;
  }
}
",
                @"
namespace CompatTests
{
  public interface IFoo
  {
    event System.EventHandler MyEvent;

    // .NET Framework doesn't support default implementations.
#if !NETFRAMEWORK
    static int MyField = 32;
    int MyMethod() => 0;
#endif
  }
}
"
            };
        }
    }
}
