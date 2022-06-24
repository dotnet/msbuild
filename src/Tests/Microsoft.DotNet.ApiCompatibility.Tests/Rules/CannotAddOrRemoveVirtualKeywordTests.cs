// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotAddOrRemoveVirtualKeywordTests
    {
        private static readonly bool IsNetFramework = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

        private static string CreateType(string s, params object[] args) => string.Format(@"
namespace CompatTests {{
  public{0} First
  {{
{1}
  }}
}}
", s, string.Join("\n", args));

        private static CompatDifference[] CreateDifferences(params (DifferenceType dt, string memberId)[] args)
        {
            var differences = new CompatDifference[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                string diagnosticId = args[i].dt == DifferenceType.Removed
                    ? DiagnosticIds.CannotRemoveVirtualFromMember
                    : DiagnosticIds.CannotAddVirtualToMember;
                differences[i] = new CompatDifference(diagnosticId, string.Empty, args[i].dt, args[i].memberId);
            }
            return differences;
        }

        public static IEnumerable<object[]> RemovedCases()
        {
            // remove virtual
            yield return new object[] {
                CreateType(" class", " public virtual void F() {}"),
                CreateType(" class", " public void F() {}"),
                false,
                CreateDifferences((DifferenceType.Removed, "M:CompatTests.First.F")),
            };
            // properties
            yield return new object[] {
                CreateType(" class", " public virtual int F { get; }"),
                CreateType(" class", " public int F { get; }"),
                false,
                CreateDifferences((DifferenceType.Removed, "P:CompatTests.First.F"),
                                    (DifferenceType.Removed, "M:CompatTests.First.get_F")),
            };
            // indexers
            yield return new object[] {
                CreateType(" class", " public virtual int this[int i] { get => i; }"),
                CreateType(" class", " public int this[int i] { get => i; }"),
                false,
                CreateDifferences((DifferenceType.Removed, "P:CompatTests.First.Item(System.Int32)"),
                                    (DifferenceType.Removed, "M:CompatTests.First.get_Item(System.Int32)")),
            };
            // events
            yield return new object[] {
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public virtual event EventHandler F;"),
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public event EventHandler F;"),
                false,
                CreateDifferences((DifferenceType.Removed, "M:CompatTests.First.add_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Removed, "M:CompatTests.First.remove_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Removed, "E:CompatTests.First.F")),
            };
        }

        public static IEnumerable<object[]> AddedCases()
        {
            // add virtual
            yield return new object[] {
                CreateType(" class", " public void F() {}"),
                CreateType(" class", " public virtual void F() {}"),
                false,
                CreateDifferences(),
            };
            // abstract -> virtual
            yield return new object[] {
                CreateType(" abstract class", " public abstract void F();"),
                CreateType(" abstract class", " public virtual void F() {}"),
                false,
                CreateDifferences(),
            };
            // properties
            yield return new object[] {
                CreateType(" class", " public int F { get; }"),
                CreateType(" class", " public virtual int F { get; }"),
                false,
                CreateDifferences(),
            };
            // indexers
            yield return new object[] {
                CreateType(" class", " public int this[int i] { get => i; }"),
                CreateType(" class", " public virtual int this[int i] { get => i; }"),
                false,
                CreateDifferences(),
            };
            // events
            yield return new object[] {
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public event EventHandler F;"),
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public virtual event EventHandler F;"),
                false,
                CreateDifferences(),
            };
        }

        public static IEnumerable<object[]> AddedCasesStrictMode()
        {
            // add virtual
            yield return new object[] {
                CreateType(" class", " public void F() {}"),
                CreateType(" class", " public virtual void F() {}"),
                true,
                CreateDifferences((DifferenceType.Added,"M:CompatTests.First.F" )),
            };
            // abstract -> virtual
            yield return new object[] {
                CreateType(" abstract class", " public abstract void F();"),
                CreateType(" abstract class", " public virtual void F() {}"),
                true,
                CreateDifferences((DifferenceType.Added, "M:CompatTests.First.F")),
            };
            // properties
            yield return new object[] {
                CreateType(" class", " public int F { get; }"),
                CreateType(" class", " public virtual int F { get; }"),
                true,
                CreateDifferences((DifferenceType.Added, "P:CompatTests.First.F"),
                                    (DifferenceType.Added, "M:CompatTests.First.get_F")),
            };
            // indexers
            yield return new object[] {
                CreateType(" class", " public int this[int i] { get => i; }"),
                CreateType(" class", " public virtual int this[int i] { get => i; }"),
                true,
                CreateDifferences((DifferenceType.Added, "P:CompatTests.First.Item(System.Int32)"),
                                    (DifferenceType.Added, "M:CompatTests.First.get_Item(System.Int32)")),
            };
            // events
            yield return new object[] {
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public event EventHandler F;"),
                CreateType(" class", " public delegate void EventHandler(object sender, object e);", " public virtual event EventHandler F;"),
                true,
                CreateDifferences((DifferenceType.Added, "M:CompatTests.First.add_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Added, "M:CompatTests.First.remove_F(CompatTests.First.EventHandler)"),
                                    (DifferenceType.Added, "E:CompatTests.First.F")),
            };
        }

        [Theory]
        [MemberData(nameof(RemovedCases))]
        [MemberData(nameof(AddedCases))]
        [MemberData(nameof(AddedCasesStrictMode))]
        public static void EnsureDiagnosticIsReported(string leftSyntax, string rightSyntax, bool strictMode, CompatDifference[] expected)
        {
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            differ.StrictMode = strictMode;
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            Assert.Equal(expected, differences);
        }

        [Fact]
        public static void EnsureNoCrashWhenMembersDoNotExist()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public class First {
    public virtual void F() {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public class First {
    public virtual void G() {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new();
            IEnumerable<CompatDifference> differences = differ.GetDifferences(new[] { left }, new[] { right });
            CompatDifference[] expected = new[]
            {
                new CompatDifference(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.F"),
            };
            Assert.Equal(expected, differences);
        }
    }
}
