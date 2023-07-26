// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotAddMemberToInterfaceTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotAddMemberToInterface(settings, context));

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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "M:CompatTests.IFoo.MyMethod"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "P:CompatTests.IFoo.MyPropertyWithoutDefaultImplementation"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "E:CompatTests.IFoo.MyEventWithoutImplementation"),
            };
            Assert.Equal(expected, differences);
        }

        [Theory]
        [MemberData(nameof(NoDifferencesShouldBeReportedData))]
        public void NoDifferencesShouldBeReported(string leftSyntax, string rightSyntax)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

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

            ApiComparer differ = new(s_ruleFactory);
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expectedDiffs =
            {
                new CompatDifference(left.MetadataInformation, right[1].MetadataInformation, DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "E:CompatTests.IFoo.MyOtherEvent"),
                new CompatDifference(left.MetadataInformation, right[2].MetadataInformation, DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Added, "M:CompatTests.IFoo.MyOtherMethod"),
            };

            Assert.Equal(expectedDiffs, differences);
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
