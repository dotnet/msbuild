// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class MembersMustExistTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new MembersMustExist(settings, context));

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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.ShouldReportMethod(System.String,System.String)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.get_ShouldReportMissingProperty"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.get_Item(System.Int32)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.add_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.remove_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.ReportMissingField"),
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
            ApiComparer differ = new(s_ruleFactory);

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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.String)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.String)"),
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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: includeInternals));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (includeInternals)
            {
                CompatDifference[] expected =
                {
                    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.Int32)"),
                    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.set_InternalProperty(System.Int32)"),
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
            ApiComparer differ = new(s_ruleFactory);

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
            ApiComparer differ = new(s_ruleFactory);

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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MyMethod(System.String)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MyOutMethod(System.String@)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MyRefMethod(System.String@)"),
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
            ApiComparer differ = new(s_ruleFactory);
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax), new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expectedDiffs =
            {
                new CompatDifference(left.MetadataInformation, right.ElementAt(0).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(0).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.FirstNested.SecondNested.MyMethod"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(1).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.FirstNested.get_MyProperty"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(2).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.FirstNested.SecondNested.MyMethod"),
            };

            Assert.Equal(expectedDiffs, differences);
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
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            string[] rightSyntaxes = new[] { leftSyntax, leftSyntax, leftSyntax, leftSyntax };
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
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
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.#ctor")
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public void NumericPtrNotFlagged()
        {
            string leftSyntax = @"
namespace CompatTests
{
  using System;

  public class First
  {
    public void F(IntPtr p) {}
    public void G(UIntPtr p) {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public void F(nint p) {}
    public void G(nuint p) {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Fact]
        public void ThisExtensionMethodModifierRemovalFlagged()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public static class First
  {
    public static void F(this string s) {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public static class First
  {
    public static void F(string s) {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Equal(new[]
            {
                // The call to GetDocumentationCommentId doesn't return a string that includes the "this" keyword.
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.F(System.String)")
            }, differences);
        }

        [Fact]
        public void MemberTypesChangeFlagged()
        {
            string leftSyntax = @"
using System;

namespace CompatTests
{
  public class First
  {
    public string S;
    public bool Prop { get; set; }
    public string M() => null;
    public delegate void SampleEventHandler(object sender, EventArgs e);
    public event SampleEventHandler E;
  }
}
";
            string rightSyntax = @"
using System;

namespace CompatTests
{
  public class First
  {
    public delegate void SampleEventHandler(object sender, EventArgs e);

    public int S;
    public string Prop { get; set; }
    public bool M() => false;
    public delegate void SampleEventHandler1(object sender, EventArgs e);
    public event SampleEventHandler1 E;
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Equal(new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.S"),
                // CompatTests.First.Prop.set isn't reported as the return types match: 'void'.
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.get_Prop"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.M")
                // CompatTests.First.E_add and CompatTests.First.E_remove aren't reported as the symbol's DisplayString doesn't include the parameter type.
            }, differences);
        }
    }
}
