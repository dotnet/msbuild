// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
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
    static MyField = 2;
    int MyPropertyWithDefaultImplementation { get { } set { } }
    event EventHandler MyEvent { add { } remove { } }
    byte MyPropertyWithoutDefaultImplementation { get; set; }
    event EventHandler MyEventWithoutImplementation;
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

            Assert.Equal(expected, differences, CompatDifferenceComparer.Default);
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
    static MyField = 2;
    int MyPropertyWithDefaultImplementation { get { } set { } }
    event EventHandler MyEvent { add { } remove { } }
    byte MyPropertyWithoutDefaultImplementation { get; set; }
    event EventHandler MyEventWithoutImplementation;
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
    event EventHandler MyEvent;
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
    event EventHandler MyEvent;
    int MyPropertyWithDIM { get => 0; set { } }
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
    event EventHandler MyEvent;
    event EventHandler MyOtherEvent;
    static int MyField = 3;
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
    string MyOtherMethodWithDIM() => string.Empty;
    int MyProperty { get; set; }
    event EventHandler MyEvent;
    event EventHandler MyOtherEventWithDIM { add { } remove { } }
    static int MyField = 3;
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
    private string MyPrivateMethod();
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
    byte MyOtherProperty { get => (byte)0; set { } }
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
    event EventHandler EventHandler;
  }
}
",
                @"
namespace CompatTests
{
  public interface IFoo
  {
    event EventHandler EventHandler;
    static int MyField = 32;
    int MyMethod() => 0;
  }
}
"
            };
        }
    }
}
