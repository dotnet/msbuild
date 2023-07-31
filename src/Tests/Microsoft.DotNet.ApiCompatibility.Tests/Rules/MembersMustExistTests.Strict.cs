// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class MembersMustExistTests_Strict
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new MembersMustExist(settings, context));

        [Fact]
        public static void MissingMembersOnLeftAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
  }
  public delegate void EventHandler(object sender, System.EventArgs e);
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
    public void ShouldReportMethod(string a, string b) { }
    public string ShouldReportMissingProperty { get; }
    public string this[int index] { get => string.Empty; }
    public event EventHandler ShouldReportMissingEvent;
    public int ReportMissingField = 0;
  }

  public delegate void EventHandler(object sender, System.EventArgs e);
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.ShouldReportMethod(System.String,System.String)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.get_ShouldReportMissingProperty"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.get_Item(System.Int32)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.add_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.remove_ShouldReportMissingEvent(CompatTests.EventHandler)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.ReportMissingField"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void HiddenMemberInRightIsNotReported()
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
  public class Second : FirstBase { }
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
  public class Second : FirstBase
  {
    public new void MyMethod() { }
    public new string MyMethodWithParams(string a, int b, FirstBase c) => string.Empty;
    public new T MyGenericMethod<T, T2, T3>(string name, T2 a, T3 b) => throw null;
    public override string MyVirtualMethod() => string.Empty;
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Empty(differences);
        }

        [Fact]
        public static void MultipleOverridesMissingInLeftAreReported()
        {
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, string b) => string.Empty;
    public string MultipleOverrides(string a, int b, string c) => string.Empty;
    public string MultipleOverrides(string a, int b, int c) => string.Empty;
  }
}
";
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, int b, int c) => string.Empty;
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MultipleOverrides(System.String,System.String)"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.String)"),
            };
            Assert.Equal(expected, differences);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void IncludeInternalsIsRespectedForMembers_IndividualAssemblies(bool includeInternals)
        {
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, string b) => string.Empty;
    public string MultipleOverrides(string a, int b, string c) => string.Empty;
    internal string MultipleOverrides(string a, int b, int c) => string.Empty;
    internal int InternalProperty { get; set; }
  }
}
";
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string MultipleOverrides() => string.Empty;
    public string MultipleOverrides(string a) => string.Empty;
    public string MultipleOverrides(string a, string b) => string.Empty;
    public string MultipleOverrides(string a, int b, string c) => string.Empty;
    internal int InternalProperty { get; }
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(
                strictMode: true,
                includeInternalSymbols: includeInternals));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            if (includeInternals)
            {
                CompatDifference[] expected =
                {
                    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MultipleOverrides(System.String,System.Int32,System.Int32)"),
                    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.set_InternalProperty(System.Int32)"),
                };
                Assert.Equal(expected, differences);
            }
            else
            {
                Assert.Empty(differences);
            }
        }

        [Fact]
        public static void MissingMembersOnBothSidesAreReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
    public string MissingMethodRight() => string.Empty;
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First
  {
    public string Parameterless() => string.Empty;
    public void MissingMethodLeft(string a, string b) { }
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.MissingMethodRight"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "M:CompatTests.First.MissingMethodLeft(System.String,System.String)"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void MultipleRightsMissingMembersOnLeftAreReported()
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
        public int MyMethod() => 0;
        public class ThirdNested
        {
        }
      }
    }
  }
}
"};
            ElementContainer<IAssemblySymbol> left = new(SymbolFactory.GetAssemblyFromSyntax(leftSyntax),
                new MetadataInformation(string.Empty, "ref"));
            IReadOnlyList<ElementContainer<IAssemblySymbol>> right = SymbolFactoryExtensions.GetElementContainersFromSyntaxes(rightSyntaxes);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expectedDiffs =
            {
                new CompatDifference(left.MetadataInformation, right.ElementAt(0).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
                new CompatDifference(left.MetadataInformation, right.ElementAt(1).MetadataInformation, DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.FirstNested.SecondNested.ThirdNested.MyField"),
            };
            Assert.Equal(expectedDiffs, differences);
        }

        [Fact]
        public static void MissingMembersOnEnumReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First
  {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public enum First
  {
    F = 5,
    E = 4,
    D = 3,
    C = 2,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: true));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected =
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.A"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "F:CompatTests.First.B"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.F"),
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Added, "F:CompatTests.First.E"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
