// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotChangeVisibilityTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotChangeVisibility(settings, context));

        /*
         * Tests for:
         * - Reduce visibility of type
         * - Expand visibility of type
         * - Expand visibility of member
         * - Restricting visibility of protected member inside sealed type
         * - Restricting visibility of protected member inside type without accessible constructor
         * - Restricting visibility of member
         * - Strict mode
         */

        public static TheoryData<string, string, CompatDifference[]> TestCases => new()
        {
            // Reduce visibility of type
            {
                @"
namespace CompatTests
{
  public class First {}
}
",
                @"
namespace CompatTests
{
  internal class First {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
}
            },
            // Reducing visibility of internal type to protected
            {
                @"
namespace CompatTests
{
  public class First {
    internal int F = 0;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    protected int F = 0;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            },
            // Reducing visibility of protected type to internal
            {
                @"
namespace CompatTests
{
  public class First {
    protected int F = 0;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    internal int F = 0;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            },
            // Expand visibility of type
            {
                @"
namespace CompatTests
{
  internal class First {}
}
",
                @"
namespace CompatTests
{
  public class First {}
}
",
new CompatDifference[] {}
            },
            // Expand visibility of member
            {
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    public int F;
  }
}
",
new CompatDifference[] {}
            },
            // Reducing visibility of protected member inside sealed type.
            // Since we don't visit private members, we don't issue a diagnostic here.
            // We suppress the warning for declaring protected members in sealed types,
            // since we want to check for a different diagnostic.
            {
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {}
            },
            // Reducing visibility of protected member inside type without accessible constructor
            // Since we don't visit private members, we don't issue a diagnostic here.
            {
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {}
            },
            // Reduce visibility of member
            {
                @"
namespace CompatTests
{
  public class First {
    public int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            }
        };

        public static TheoryData<string, string, CompatDifference[]> StrictMode => new()
        {
            // Reduce visibility of type
            {
                @"
namespace CompatTests
{
  public class First {}
}
",
                @"
namespace CompatTests
{
  internal class First {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
}
            },
            // Expand visibility of type
            {
                @"
namespace CompatTests
{
  internal class First {}
}
",
                @"
namespace CompatTests
{
  public class First {}
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotExpandVisibility, string.Empty, DifferenceType.Changed, "T:CompatTests.First")
}
            },
            // Expand visibility of member
            {
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    public int F;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotExpandVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            },
            // Reducing visibility of protected member inside sealed type
            // Since we don't visit private members, we don't issue a diagnostic here.
            // We suppress the warning for declaring protected members in sealed types,
            // since we want to check for a different diagnostic.
            {
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public sealed class First {

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {}
            },
            // Reducing visibility of protected member inside type without accessible constructor
            // Since we don't visit private members, we don't issue a diagnostic here.
            {
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0628
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    private First() {}

#pragma warning disable CS0169
    private int F;
  }
}
",
new CompatDifference[] {}
            },
            // Reduce visibility of member
            {
                @"
namespace CompatTests
{
  public class First {
    public int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
new CompatDifference[] {
    CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotReduceVisibility, string.Empty, DifferenceType.Changed, "F:CompatTests.First.F")
}
            }
        };

        public static TheoryData<string, string, CompatDifference[]> NoInternals => new()
        {
            // No diagnostic on expanding visibility of type from internal to public
            {
                @"
namespace CompatTests
{
  internal class First {}
}
",
                @"
namespace CompatTests
{
  public class First {}
}
",
new CompatDifference[] {}
            },
            // No diagnostic on expanding visibility of member from protected to protected internal
            {
                @"
namespace CompatTests
{
  public class First {
    protected int F;
  }
}
",
                @"
namespace CompatTests
{
  public class First {
    protected internal int F;
  }
}
",
new CompatDifference[] {}
            },
        };

        [Theory]
        [MemberData(nameof(TestCases))]
        public void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(includeInternalSymbols: true));

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(StrictMode))]
        public void EnsureDiagnosticIsReportedInStrictMode(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(
                includeInternalSymbols: true,
                strictMode: true));

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(NoInternals))]
        public void EnsureReportedInStrictModeWithoutInternalSymbols(string leftSyntax, string rightSyntax, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(
                includeInternalSymbols: false,
                strictMode: true));

            IEnumerable<CompatDifference> actual = differ.GetDifferences(left, right);

            Assert.Equal(expected, actual);
        }
    }
}
