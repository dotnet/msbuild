// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class ParameterNamesCannotChangeTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotChangeParameterName(settings, context));

        public static TheoryData<string, string, CompatDifference[]> TestCases => new()
        {
            // Method
            {
                 @"
namespace CompatTests
{
  public class First {
    public void F(int a, string s) {}
  }
}
",
                 @"
namespace CompatTests
{
  public class First {
    public void F(int b, string t) {}
  }
}
",
                 new CompatDifference[] {
                     CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeParameterName, "", DifferenceType.Changed, "M:CompatTests.First.F(System.Int32,System.String)$0"),
                     CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeParameterName, "", DifferenceType.Changed, "M:CompatTests.First.F(System.Int32,System.String)$1")
                 }
            },
            // Constructor
            {
                 @"
namespace CompatTests
{
  public class First {
    public First(int a, string s) {}
  }
}
",
                 @"
namespace CompatTests
{
  public class First {
    public First(int b, string t) {}
  }
}
",
                 new CompatDifference[] {
                     CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeParameterName, "", DifferenceType.Changed, "M:CompatTests.First.#ctor(System.Int32,System.String)$0"),
                     CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeParameterName, "", DifferenceType.Changed, "M:CompatTests.First.#ctor(System.Int32,System.String)$1")
                 }
            },
            // Property
            {
                 @"
namespace CompatTests
{
  public class First {
    public int F { get; }
  }
}
",
                 @"
namespace CompatTests
{
  public class First {
    public int F { get; }
  }
}
",
                 new CompatDifference[] {}
            }
        };

        [Theory]
        [MemberData(nameof(TestCases))]
        public void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }
    }
}
