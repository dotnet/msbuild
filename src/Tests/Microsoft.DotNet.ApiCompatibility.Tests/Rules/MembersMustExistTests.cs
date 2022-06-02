// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class MembersMustExistTests
    {
        [Fact]
        public static void MissingMembersAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public void Parameterless() { }
    public void ShouldReportMethod(string a, string b) { }
    public string ShouldReportMissingProperty { get; }
    public string this[int index] { get => string.Empty; }
    public event EventHandler ShouldReportMissingEvent;
    public int ReportMissingField = 0;
  }

  public delegate void EventHandler(object sender, System.EventArgs e);
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public void Parameterless() { }
  }
  public delegate void EventHandler(object sender, System.EventArgs e);
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.ShouldReportMethod(System.String,System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.get_ShouldReportMissingProperty"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.get_Item(System.Int32)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.add_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.remove_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.ReportMissingField"),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void HiddenMemberInLeftIsNotReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class FirstBase
  {
    public void MyMethod() { }
    public string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public virtual string MyVirtualMethod() => string.Empty;
  }
  public class Second : FirstBase
  {
    public new void MyMethod() { }
    public new string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public new T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public override string MyVirtualMethod() => string.Empty;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class FirstBase
  {
    public void MyMethod() { }
    public string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public virtual string MyVirtualMethod() => string.Empty;
  }
  public class Second : FirstBase { }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            Assert.Empty(differences);
        }

        [Fact]
        public static void MultipleOverridesAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => a;
    public string MultipleOverrides(string a, string b) => b;
    public string MultipleOverrides(string a, int b, string c) => c;
    public string MultipleOverrides(string a, int b, int c) => a;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => a;
    public string MultipleOverrides(string a, int b, int c) => a;
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.String)"),
            };

            Assert.Equal(expected, differences);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void IncludeInternalsIsRespectedForMembers_IndividualAssemblies(bool includeInternals)
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => a;
    public string MultipleOverrides(string a, string b) => b;
    public string MultipleOverrides(string a, int b, string c) => c;
    internal string MultipleOverrides(string a, int b, int c) => a;
    internal int InternalProperty { get; set; }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => a;
    public string MultipleOverrides(string a, string b) => b;
    public string MultipleOverrides(string a, int b, string c) => c;
    internal int InternalProperty { get; }
  }
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
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.Int32)"),
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.set_InternalProperty(System.Int32)"),
                };

                Assert.Equal(expected, differences);
            }
            else
            {
                Assert.Empty(differences);
            }
        }

        [Fact]
        public static void MembersWithDifferentNullableAnnotationsNoErrors()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string? MyMethod(string? a) => null;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MyMethod(string a) => null;
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Fact]
        public static void ParametersWithDifferentModifiersNoErrors()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MyMethod(ref string a) => throw null;
    public void MyOutMethod(out string a) => throw null;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MyMethod(string a) => throw null;
    public void MyOutMethod(string a) { }
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Fact]
        public static void ParametersWithDifferentModifiersReportedWhenMissing()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MyMethod(string? a) => throw null!;
    public void MyOutMethod(out string a) => throw null!;
    public void MyRefMethod(ref string a) { }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public void OneMethod() { }
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MyMethod(System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MyOutMethod(System.String@)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MyRefMethod(System.String@)"),
            };

            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingMembersAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public string MyProperty { get; }
      public class SecondNested
      {
        public int MyMethod() => 0;
        public class ThirdNested
        {
          public string MyField;
        }
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
      public string MyProperty { get; }
      public class SecondNested
      {
        public class ThirdNested
        {
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
        public int MyMethod() => 0;
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
      public string MyProperty { get; }
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
"};

            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, string.Empty, "ref"));

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            CompatDifference[][] expectedDiffs =
            {
                new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.FirstNested.SecondNested.MyMethod"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.FirstNested.get_MyProperty"),
                },
                new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.FirstNested.SecondNested.MyMethod"),
                },
            };

            AssertExtensions.MultiRightResult(left.MetadataInformation, expectedDiffs, differences);
        }

        [Fact]
        public static void MultipleRightsNoDifferencesReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public class FirstNested
    {
      public string MyProperty { get; }
      public class SecondNested
      {
        public int MyMethod() => 0;
        public class ThirdNested
        {
          public string MyField;
        }
      }
    }
  }
}
";

            string[] rightSyntaxes = new[] { leftSyntax, leftSyntax, leftSyntax, leftSyntax };
            MetadataInformation expectedLeftMetadata = new(string.Empty, string.Empty, "ref");
            ApiComparer differ = new();
            ElementContainer<IAssemblySymbol> left =
                new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), expectedLeftMetadata);

            IList<ElementContainer<IAssemblySymbol>> right = SymbolFactory.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                differ.GetDifferences(left, right);

            AssertExtensions.MultiRightEmptyDifferences(expectedLeftMetadata, rightSyntaxes.Length, differences);
        }



        [Fact]
        public void ParameterlessConstructorRemovalIsReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public sealed class First
  {
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    private First() { }
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);

            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
            new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.#ctor")
            };

            Assert.Equal(expected, differences);
        }
    }
}
