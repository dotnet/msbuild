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
            ApiComparer differ = new(includeInternalSymbols: includeInternals);
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
    }
}
