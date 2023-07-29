// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class EnumsMustMatchTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new EnumsMustMatch(settings, context));

        [Fact]
        public static void DifferencesReported()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First {
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
  public enum First {
    E = 4,
    D = 3,
    C = 2,
    B = 1,
    A = 1,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.A"),
            };
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void RemovedEnum()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First {
    A = 0,
    B = 1,
    C = 2,
    D = 3,
  }
  public enum Second {}
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public enum First {
    D = 3,
    C = 2,
    B = 1,
    A = 0,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            // Register EnumMustMatch and MemberMustExist rules as this test validates both.
            ApiComparer differ = new(s_ruleFactory.WithRule((settings, context) => new MembersMustExist(settings, context)));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.NotEmpty(differences);
        }

        [Fact]
        public static void AddedEnum()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First {
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
  public enum First {
    D = 3,
    C = 2,
    B = 1,
    A = 0,
  }
  public enum Second {}
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            Assert.Empty(differences);
        }

        [Fact]
        public static void BackingStoreChanged()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public enum First : short {
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
  public enum First : int {
    D = 3,
    C = 2,
    B = 1,
    A = 0,
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.EnumTypesMustMatch, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
