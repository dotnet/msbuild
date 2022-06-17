// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class EnumsMustMatchTests
    {
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
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.EnumValuesMustMatch, string.Empty, DifferenceType.Changed, "F:CompatTests.First.A"),
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
            ApiComparer differ = new();
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
            ApiComparer differ = new();
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
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.EnumTypesMustMatch, string.Empty, DifferenceType.Changed, "T:CompatTests.First"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
