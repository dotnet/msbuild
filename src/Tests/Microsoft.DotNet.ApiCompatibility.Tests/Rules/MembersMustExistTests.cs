// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests.Rules
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
    public string Parameterless() { }
    public void ShouldReportMethod(string a, string b) { }
    public string ShouldReportMissingProperty { get; }
    public string this[int index] { get; }
    public event EventHandler ShouldReportMissingEvent;
    public int ReportMissingField = 0;
  }

  public delegate void EventHandler(object sender EventArgs e);
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() { }
  }
  public delegate void EventHandler(object sender EventArgs e);
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.ShouldReportMethod(string, string)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.ShouldReportMethod(System.String,System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.ShouldReportMissingProperty.get' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.get_ShouldReportMissingProperty"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.this[int].get' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.get_Item(System.Int32)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.ShouldReportMissingEvent.add' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.add_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.ShouldReportMissingEvent.remove' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.remove_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.ReportMissingField' exists on the left but not on the right", DifferenceType.Removed, "F:CompatTests.First.ReportMissingField"),
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
    public string MyMethodWithParams(string a, int b, FirstBase c) { }
    public T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) { }
    public virtual string MyVirtualMethod() { }
  }
  public class Second : FirstBase
  {
    public new void MyMethod() { }
    public new string MyMethodWithParams(string a, int b, FirstBase c) { }
    public new T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) { }
    public override string MyVirtualMethod() { }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class FirstBase
  {
    public void MyMethod() { }
    public string MyMethodWithParams(string a, int b, FirstBase c) { }
    public T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) { }
    public virtual string MyVirtualMethod() { }
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
        public static void NoDifferencesWithNoWarn()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public void MissingMember() { }
    public int MissingProperty { get; }
    public int MissingField;
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.NoWarn = DiagnosticIds.MemberMustExist;
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
    public string MultipleOverrides() { }
    public string MultipleOverrides(string a) { }
    public string MultipleOverrides(string a, string b) { }
    public string MultipleOverrides(string a, int b, string c) { }
    public string MultipleOverrides(string a, int b, int c) { }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() { }
    public string MultipleOverrides(string a) { }
    public string MultipleOverrides(string a, int b, int c) { }
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.MultipleOverrides(string, string)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.MultipleOverrides(string, int, string)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.String)"),
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
    public string MultipleOverrides() { }
    public string MultipleOverrides(string a) { }
    public string MultipleOverrides(string a, string b) { }
    public string MultipleOverrides(string a, int b, string c) { }
    internal string MultipleOverrides(string a, int b, int c) { }
    internal int InternalProperty { get; set; }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() { }
    public string MultipleOverrides(string a) { }
    public string MultipleOverrides(string a, string b) { }
    public string MultipleOverrides(string a, int b, string c) { }
    internal int InternalProperty { get; }
  }
}
";

            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax, assemblyName: "DifferentName");
            ApiComparer differ = new();
            differ.IncludeInternalSymbols = includeInternals;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (includeInternals)
            {
                CompatDifference[] expected = new[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.MultipleOverrides(string, int, int)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.Int32)"),
                    new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.InternalProperty.set' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.set_InternalProperty(System.Int32)"),
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
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable: true, includeDefaultReferences: true);
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
    public string MyMethod(string? a) => throw null;
    public void MyOutMethod(out string a) => throw null;
    public void MyRefMethod(ref string a) { }
  }
}
";

            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public void OneMethod => { }
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax, enableNullable: true, includeDefaultReferences: true);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.MyMethod(string?)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.MyMethod(System.String)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.MyOutMethod(out string)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.MyOutMethod(System.String@)"),
                new CompatDifference(DiagnosticIds.MemberMustExist, "Member 'CompatTests.First.MyRefMethod(ref string)' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.MyRefMethod(System.String@)"),
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

            List<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> expected = new()
            {
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-0"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, $"Member 'CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField' exists on the left but not on the right", DifferenceType.Removed, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
                    new CompatDifference(DiagnosticIds.MemberMustExist, $"Member 'CompatTests.First.FirstNested.SecondNested.MyMethod()' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.FirstNested.SecondNested.MyMethod"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-1"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, $"Member 'CompatTests.First.FirstNested.MyProperty.get' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.FirstNested.get_MyProperty"),
                }),
                (left.MetadataInformation, new MetadataInformation(string.Empty, string.Empty, "runtime-2"), new CompatDifference[]
                {
                    new CompatDifference(DiagnosticIds.MemberMustExist, $"Member 'CompatTests.First.FirstNested.SecondNested.MyMethod()' exists on the left but not on the right", DifferenceType.Removed, "M:CompatTests.First.FirstNested.SecondNested.MyMethod"),
                }),
            };

            Assert.Equal(expected, differences);
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

            int i = 0;
            foreach ((MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences) diff in differences)
            {
                Assert.Equal(expectedLeftMetadata, diff.left);
                MetadataInformation expectedRightMetadata = new(string.Empty, string.Empty, $"runtime-{i++}");
                Assert.Equal(expectedRightMetadata, diff.right);
                Assert.Empty(diff.differences);
            }

            Assert.Equal(4, i);
        }
    }
}
