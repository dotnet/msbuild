// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Rules.Tests
{
    public class CannotAddOrRemoveVirtualKeywordTests
    {
        private static readonly TestRuleFactory s_ruleFactory = new((settings, context) => new CannotAddOrRemoveVirtualKeyword(settings, context));

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
                differences[i] = CompatDifference.CreateWithDefaultMetadata(
                    args[i].dt == DifferenceType.Removed ? DiagnosticIds.CannotRemoveVirtualFromMember : DiagnosticIds.CannotAddVirtualToMember,
                    string.Empty,
                    args[i].dt,
                    args[i].memberId);
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
            // effectively sealed containing type
            yield return new object[] {
                CreateType(" class", "private First() {}", " public virtual void F() {}"),
                CreateType(" class", "private First() {}", " public void F() {}"),
                false,
                CreateDifferences(),
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
            ApiComparer differ = new(s_ruleFactory, new ApiComparerSettings(strictMode: strictMode));

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
            // Register CannotAddOrRemoveVirtualKeyword and MemberMustExist rules as this test validates both.
            ApiComparer differ = new(s_ruleFactory.WithRule((settings, context) => new MembersMustExist(settings, context)));

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            CompatDifference[] expected = new[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.MemberMustExist, string.Empty, DifferenceType.Removed, "M:CompatTests.First.F"),
            };
            Assert.Equal(expected, differences);
        }

        // Don't run this test on .NET Framework, because default interface methods weren't introduced until C# 8.
#if !NETFRAMEWORK
        [Fact]
        public static void EnsureDiagnosticWhenAddingSealedToInterfaceMember()
        {
            string leftSyntax = @"
namespace CompatTests
{
  public interface First {
    public void F() {}
  }
}
";
            string rightSyntax = @"
namespace CompatTests
{
  public interface First {
    public sealed void F() {}
  }
}
";
            IAssemblySymbol left = SymbolFactory.GetAssemblyFromSyntax(leftSyntax);
            IAssemblySymbol right = SymbolFactory.GetAssemblyFromSyntax(rightSyntax);
            ApiComparer differ = new(s_ruleFactory);

            IEnumerable<CompatDifference> differences = differ.GetDifferences(left, right);

            Assert.Equal(new CompatDifference[]
            {
                CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddSealedToInterfaceMember, string.Empty, DifferenceType.Added, "M:CompatTests.First.F")
            }, differences);
        }
#endif
    }
}
