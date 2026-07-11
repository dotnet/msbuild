// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure.EditorConfig;
using static Microsoft.Build.Experimental.BuildCheck.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

#nullable disable

namespace Microsoft.Build.BuildCheck.UnitTests;

[TestClass]
public class EditorConfig_Tests
{

    #region AssertEqualityComparer<T>
    private sealed class AssertEqualityComparer<T> : IEqualityComparer<T>
    {
        public static readonly IEqualityComparer<T> Instance = new AssertEqualityComparer<T>();

        private static bool CanBeNull()
        {
            var type = typeof(T);
            return !type.IsValueType ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static bool IsNull(T @object)
        {
            if (!CanBeNull())
            {
                return false;
            }

            return object.Equals(@object, default(T));
        }

        public static bool Equals(T left, T right)
        {
            return Instance.Equals(left, right);
        }

        bool IEqualityComparer<T>.Equals(T x, T y)
        {
            if (CanBeNull())
            {
                if (object.Equals(x, default(T)))
                {
                    return object.Equals(y, default(T));
                }

                if (object.Equals(y, default(T)))
                {
                    return false;
                }
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            if (x is IEquatable<T> equatable)
            {
                return equatable.Equals(y);
            }

            if (x is IComparable<T> comparableT)
            {
                return comparableT.CompareTo(y) == 0;
            }

            if (x is IComparable comparable)
            {
                return comparable.CompareTo(y) == 0;
            }

            var enumerableX = x as IEnumerable;
            var enumerableY = y as IEnumerable;

            if (enumerableX != null && enumerableY != null)
            {
                var enumeratorX = enumerableX.GetEnumerator();
                var enumeratorY = enumerableY.GetEnumerator();

                while (true)
                {
                    bool hasNextX = enumeratorX.MoveNext();
                    bool hasNextY = enumeratorY.MoveNext();

                    if (!hasNextX || !hasNextY)
                    {
                        return hasNextX == hasNextY;
                    }

                    if (!Equals(enumeratorX.Current, enumeratorY.Current))
                    {
                        return false;
                    }
                }
            }

            return object.Equals(x, y);
        }

        int IEqualityComparer<T>.GetHashCode(T obj)
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    // Section Matchin Test cases: https://github.com/dotnet/roslyn/blob/ba163e712b01358a217065eec8a4a82f94a7efd5/src/Compilers/Core/CodeAnalysisTest/Analyzers/AnalyzerConfigTests.cs#L337
    #region Section Matching Tests
    [MSBuildTestMethod]
    public void SimpleNameMatch()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("abc").Value;
        Assert.AreEqual("^.*/abc$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc"));
        Assert.IsFalse(matcher.IsMatch("/aabc"));
        Assert.IsFalse(matcher.IsMatch("/ abc"));
        Assert.IsFalse(matcher.IsMatch("/cabc"));
    }

    [MSBuildTestMethod]
    public void StarOnlyMatch()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("*").Value;
        Assert.AreEqual("^.*/[^/]*$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc"));
        Assert.IsTrue(matcher.IsMatch("/123"));
        Assert.IsTrue(matcher.IsMatch("/abc/123"));
    }

    [MSBuildTestMethod]
    public void StarNameMatch()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("*.cs").Value;
        Assert.AreEqual("^.*/[^/]*\\.cs$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc.cs"));
        Assert.IsTrue(matcher.IsMatch("/123.cs"));
        Assert.IsTrue(matcher.IsMatch("/dir/subpath.cs"));
        // Only '/' is defined as a directory separator, so the caller
        // is responsible for converting any other machine directory
        // separators to '/' before matching
        Assert.IsTrue(matcher.IsMatch("/dir\\subpath.cs"));

        Assert.IsFalse(matcher.IsMatch("/abc.vb"));
    }

    [MSBuildTestMethod]
    public void StarStarNameMatch()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("**.cs").Value;
        Assert.AreEqual("^.*/.*\\.cs$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc.cs"));
        Assert.IsTrue(matcher.IsMatch("/dir/subpath.cs"));
    }

    [MSBuildTestMethod]
    public void EscapeDot()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("...").Value;
        Assert.AreEqual("^.*/\\.\\.\\.$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/..."));
        Assert.IsTrue(matcher.IsMatch("/subdir/..."));
        Assert.IsFalse(matcher.IsMatch("/aaa"));
        Assert.IsFalse(matcher.IsMatch("/???"));
        Assert.IsFalse(matcher.IsMatch("/abc"));
    }

    [MSBuildTestMethod]
    public void EndBackslashMatch()
    {
        SectionNameMatcher? matcher = TryCreateSectionNameMatcher("abc\\");
        Assert.IsNull(matcher);
    }

    [MSBuildTestMethod]
    public void QuestionMatch()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab?def").Value;
        Assert.AreEqual("^.*/ab.def$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abcdef"));
        Assert.IsTrue(matcher.IsMatch("/ab?def"));
        Assert.IsTrue(matcher.IsMatch("/abzdef"));
        Assert.IsTrue(matcher.IsMatch("/ab/def"));
        Assert.IsTrue(matcher.IsMatch("/ab\\def"));
    }

    [MSBuildTestMethod]
    public void LiteralBackslash()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab\\\\c").Value;
        Assert.AreEqual("^.*/ab\\\\c$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/ab\\c"));
        Assert.IsFalse(matcher.IsMatch("/ab/c"));
        Assert.IsFalse(matcher.IsMatch("/ab\\\\c"));
    }

    [MSBuildTestMethod]
    public void LiteralStars()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("\\***\\*\\**").Value;
        Assert.AreEqual("^.*/\\*.*\\*\\*[^/]*$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/*ab/cd**efg*"));
        Assert.IsFalse(matcher.IsMatch("/ab/cd**efg*"));
        Assert.IsFalse(matcher.IsMatch("/*ab/cd*efg*"));
        Assert.IsFalse(matcher.IsMatch("/*ab/cd**ef/gh"));
    }

    [MSBuildTestMethod]
    public void LiteralQuestions()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("\\??\\?*\\??").Value;
        Assert.AreEqual("^.*/\\?.\\?[^/]*\\?.$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/?a?cde?f"));
        Assert.IsTrue(matcher.IsMatch("/???????f"));
        Assert.IsFalse(matcher.IsMatch("/aaaaaaaa"));
        Assert.IsFalse(matcher.IsMatch("/aa?cde?f"));
        Assert.IsFalse(matcher.IsMatch("/?a?cdexf"));
        Assert.IsFalse(matcher.IsMatch("/?axcde?f"));
    }

    [MSBuildTestMethod]
    public void LiteralBraces()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("abc\\{\\}def").Value;
        Assert.AreEqual(@"^.*/abc\{}def$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc{}def"));
        Assert.IsTrue(matcher.IsMatch("/subdir/abc{}def"));
        Assert.IsFalse(matcher.IsMatch("/abcdef"));
        Assert.IsFalse(matcher.IsMatch("/abc}{def"));
    }

    [MSBuildTestMethod]
    public void LiteralComma()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("abc\\,def").Value;
        Assert.AreEqual("^.*/abc,def$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc,def"));
        Assert.IsTrue(matcher.IsMatch("/subdir/abc,def"));
        Assert.IsFalse(matcher.IsMatch("/abcdef"));
        Assert.IsFalse(matcher.IsMatch("/abc\\,def"));
        Assert.IsFalse(matcher.IsMatch("/abc`def"));
    }

    [MSBuildTestMethod]
    public void SimpleChoice()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("*.{cs,vb,fs}").Value;
        Assert.AreEqual("^.*/[^/]*\\.(?:cs|vb|fs)$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc.cs"));
        Assert.IsTrue(matcher.IsMatch("/abc.vb"));
        Assert.IsTrue(matcher.IsMatch("/abc.fs"));
        Assert.IsTrue(matcher.IsMatch("/subdir/abc.cs"));
        Assert.IsTrue(matcher.IsMatch("/subdir/abc.vb"));
        Assert.IsTrue(matcher.IsMatch("/subdir/abc.fs"));

        Assert.IsFalse(matcher.IsMatch("/abcxcs"));
        Assert.IsFalse(matcher.IsMatch("/abcxvb"));
        Assert.IsFalse(matcher.IsMatch("/abcxfs"));
        Assert.IsFalse(matcher.IsMatch("/subdir/abcxcs"));
        Assert.IsFalse(matcher.IsMatch("/subdir/abcxcb"));
        Assert.IsFalse(matcher.IsMatch("/subdir/abcxcs"));
    }

    [MSBuildTestMethod]
    public void OneChoiceHasSlashes()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("{*.cs,subdir/test.vb}").Value;
        // This is an interesting case that may be counterintuitive.  A reasonable understanding
        // of the section matching could interpret the choice as generating multiple identical
        // sections, so [{a, b, c}] would be equivalent to [a] ... [b] ... [c] with all of the
        // same properties in each section. This is somewhat true, but the rules of how the matching
        // prefixes are constructed violate this assumption because they are defined as whether or
        // not a section contains a slash, not whether any of the choices contain a slash. So while
        // [*.cs] usually translates into '**/*.cs' because it contains no slashes, the slashes in
        // the second choice make this into '/*.cs', effectively matching only files in the root
        // directory of the match, instead of all subdirectories.
        Assert.AreEqual("^/(?:[^/]*\\.cs|subdir/test\\.vb)$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/test.cs"));
        Assert.IsTrue(matcher.IsMatch("/subdir/test.vb"));

        Assert.IsFalse(matcher.IsMatch("/subdir/test.cs"));
        Assert.IsFalse(matcher.IsMatch("/subdir/subdir/test.vb"));
        Assert.IsFalse(matcher.IsMatch("/test.vb"));
    }

    [MSBuildTestMethod]
    public void EmptyChoice()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("{}").Value;
        Assert.AreEqual("^.*/(?:)$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/"));
        Assert.IsTrue(matcher.IsMatch("/subdir/"));
        Assert.IsFalse(matcher.IsMatch("/."));
        Assert.IsFalse(matcher.IsMatch("/anything"));
    }

    [MSBuildTestMethod]
    public void SingleChoice()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("{*.cs}").Value;
        Assert.AreEqual("^.*/(?:[^/]*\\.cs)$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/test.cs"));
        Assert.IsTrue(matcher.IsMatch("/subdir/test.cs"));
        Assert.IsFalse(matcher.IsMatch("test.vb"));
        Assert.IsFalse(matcher.IsMatch("testxcs"));
    }

    [MSBuildTestMethod]
    public void UnmatchedBraces()
    {
        SectionNameMatcher? matcher = TryCreateSectionNameMatcher("{{{{}}");
        Assert.IsNull(matcher);
    }

    [MSBuildTestMethod]
    public void CommaOutsideBraces()
    {
        SectionNameMatcher? matcher = TryCreateSectionNameMatcher("abc,def");
        Assert.IsNull(matcher);
    }

    [MSBuildTestMethod]
    public void RecursiveChoice()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("{test{.cs,.vb},other.{a{bb,cc}}}").Value;
        Assert.AreEqual("^.*/(?:test(?:\\.cs|\\.vb)|other\\.(?:a(?:bb|cc)))$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/test.cs"));
        Assert.IsTrue(matcher.IsMatch("/test.vb"));
        Assert.IsTrue(matcher.IsMatch("/subdir/test.cs"));
        Assert.IsTrue(matcher.IsMatch("/subdir/test.vb"));
        Assert.IsTrue(matcher.IsMatch("/other.abb"));
        Assert.IsTrue(matcher.IsMatch("/other.acc"));

        Assert.IsFalse(matcher.IsMatch("/test.fs"));
        Assert.IsFalse(matcher.IsMatch("/other.bbb"));
        Assert.IsFalse(matcher.IsMatch("/other.ccc"));
        Assert.IsFalse(matcher.IsMatch("/subdir/other.bbb"));
        Assert.IsFalse(matcher.IsMatch("/subdir/other.ccc"));
    }

    [MSBuildTestMethod]
    public void DashChoice()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab{-}cd{-,}ef").Value;
        Assert.AreEqual("^.*/ab(?:-)cd(?:-|)ef$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/ab-cd-ef"));
        Assert.IsTrue(matcher.IsMatch("/ab-cdef"));

        Assert.IsFalse(matcher.IsMatch("/abcdef"));
        Assert.IsFalse(matcher.IsMatch("/ab--cd-ef"));
        Assert.IsFalse(matcher.IsMatch("/ab--cd--ef"));
    }

    [MSBuildTestMethod]
    public void MiddleMatch()
    {
        SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab{cs,vb,fs}cd").Value;
        Assert.AreEqual("^.*/ab(?:cs|vb|fs)cd$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abcscd"));
        Assert.IsTrue(matcher.IsMatch("/abvbcd"));
        Assert.IsTrue(matcher.IsMatch("/abfscd"));

        Assert.IsFalse(matcher.IsMatch("/abcs"));
        Assert.IsFalse(matcher.IsMatch("/abcd"));
        Assert.IsFalse(matcher.IsMatch("/vbcd"));
    }

    private static IEnumerable<(string, string)> RangeAndInverse(string s1, string s2)
    {
        yield return (s1, s2);
        yield return (s2, s1);
    }

    [MSBuildTestMethod]
    public void NumberMatch()
    {
        foreach (var (i1, i2) in RangeAndInverse("0", "10"))
        {
            var matcher = TryCreateSectionNameMatcher($"{{{i1}..{i2}}}").Value;

            Assert.IsTrue(matcher.IsMatch("/0"));
            Assert.IsTrue(matcher.IsMatch("/10"));
            Assert.IsTrue(matcher.IsMatch("/5"));
            Assert.IsTrue(matcher.IsMatch("/000005"));
            Assert.IsFalse(matcher.IsMatch("/-1"));
            Assert.IsFalse(matcher.IsMatch("/-00000001"));
            Assert.IsFalse(matcher.IsMatch("/11"));
        }
    }

    [MSBuildTestMethod]
    public void NumberMatchNegativeRange()
    {
        foreach (var (i1, i2) in RangeAndInverse("-10", "0"))
        {
            var matcher = TryCreateSectionNameMatcher($"{{{i1}..{i2}}}").Value;

            Assert.IsTrue(matcher.IsMatch("/0"));
            Assert.IsTrue(matcher.IsMatch("/-10"));
            Assert.IsTrue(matcher.IsMatch("/-5"));
            Assert.IsFalse(matcher.IsMatch("/1"));
            Assert.IsFalse(matcher.IsMatch("/-11"));
            Assert.IsFalse(matcher.IsMatch("/--0"));
        }
    }

    [MSBuildTestMethod]
    public void NumberMatchNegToPos()
    {
        foreach (var (i1, i2) in RangeAndInverse("-10", "10"))
        {
            var matcher = TryCreateSectionNameMatcher($"{{{i1}..{i2}}}").Value;

            Assert.IsTrue(matcher.IsMatch("/0"));
            Assert.IsTrue(matcher.IsMatch("/-5"));
            Assert.IsTrue(matcher.IsMatch("/5"));
            Assert.IsTrue(matcher.IsMatch("/-10"));
            Assert.IsTrue(matcher.IsMatch("/10"));
            Assert.IsFalse(matcher.IsMatch("/-11"));
            Assert.IsFalse(matcher.IsMatch("/11"));
            Assert.IsFalse(matcher.IsMatch("/--0"));
        }
    }

    [MSBuildTestMethod]
    public void MultipleNumberRanges()
    {
        foreach (var matchString in new[] { "a{-10..0}b{0..10}", "a{0..-10}b{10..0}" })
        {
            var matcher = TryCreateSectionNameMatcher(matchString).Value;

            Assert.IsTrue(matcher.IsMatch("/a0b0"));
            Assert.IsTrue(matcher.IsMatch("/a-5b0"));
            Assert.IsTrue(matcher.IsMatch("/a-5b5"));
            Assert.IsTrue(matcher.IsMatch("/a-5b10"));
            Assert.IsTrue(matcher.IsMatch("/a-10b10"));
            Assert.IsTrue(matcher.IsMatch("/a-10b0"));
            Assert.IsTrue(matcher.IsMatch("/a-0b0"));
            Assert.IsTrue(matcher.IsMatch("/a-0b-0"));

            Assert.IsFalse(matcher.IsMatch("/a-11b10"));
            Assert.IsFalse(matcher.IsMatch("/a-11b10"));
            Assert.IsFalse(matcher.IsMatch("/a-10b11"));
        }
    }

    [MSBuildTestMethod]
    public void BadNumberRanges()
    {
        var matcherOpt = TryCreateSectionNameMatcher("{0..");

        Assert.IsNull(matcherOpt);

        var matcher = TryCreateSectionNameMatcher("{0..}").Value;

        Assert.IsTrue(matcher.IsMatch("/0.."));
        Assert.IsFalse(matcher.IsMatch("/0"));
        Assert.IsFalse(matcher.IsMatch("/0."));
        Assert.IsFalse(matcher.IsMatch("/0abc"));

        matcher = TryCreateSectionNameMatcher("{0..A}").Value;
        Assert.IsTrue(matcher.IsMatch("/0..A"));
        Assert.IsFalse(matcher.IsMatch("/0"));
        Assert.IsFalse(matcher.IsMatch("/0abc"));

        // The reference implementation uses atoi here so we can presume
        // numbers out of range of Int32 are not well supported
        matcherOpt = TryCreateSectionNameMatcher($"{{0..{UInt32.MaxValue}}}");

        Assert.IsNull(matcherOpt);
    }

    [MSBuildTestMethod]
    public void CharacterClassSimple()
    {
        var matcher = TryCreateSectionNameMatcher("*.[cf]s").Value;
        Assert.AreEqual(@"^.*/[^/]*\.[cf]s$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc.cs"));
        Assert.IsTrue(matcher.IsMatch("/abc.fs"));
        Assert.IsFalse(matcher.IsMatch("/abc.vs"));
    }

    [MSBuildTestMethod]
    public void CharacterClassNegative()
    {
        var matcher = TryCreateSectionNameMatcher("*.[!cf]s").Value;
        Assert.AreEqual(@"^.*/[^/]*\.[^cf]s$", matcher.Regex.ToString());

        Assert.IsFalse(matcher.IsMatch("/abc.cs"));
        Assert.IsFalse(matcher.IsMatch("/abc.fs"));
        Assert.IsTrue(matcher.IsMatch("/abc.vs"));
        Assert.IsTrue(matcher.IsMatch("/abc.xs"));
        Assert.IsFalse(matcher.IsMatch("/abc.vxs"));
    }

    [MSBuildTestMethod]
    public void CharacterClassCaret()
    {
        var matcher = TryCreateSectionNameMatcher("*.[^cf]s").Value;
        Assert.AreEqual(@"^.*/[^/]*\.[\^cf]s$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/abc.cs"));
        Assert.IsTrue(matcher.IsMatch("/abc.fs"));
        Assert.IsTrue(matcher.IsMatch("/abc.^s"));
        Assert.IsFalse(matcher.IsMatch("/abc.vs"));
        Assert.IsFalse(matcher.IsMatch("/abc.xs"));
        Assert.IsFalse(matcher.IsMatch("/abc.vxs"));
    }

    [MSBuildTestMethod]
    public void CharacterClassRange()
    {
        var matcher = TryCreateSectionNameMatcher("[0-9]x").Value;
        Assert.AreEqual("^.*/[0-9]x$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/0x"));
        Assert.IsTrue(matcher.IsMatch("/1x"));
        Assert.IsTrue(matcher.IsMatch("/9x"));
        Assert.IsFalse(matcher.IsMatch("/yx"));
        Assert.IsFalse(matcher.IsMatch("/00x"));
    }

    [MSBuildTestMethod]
    public void CharacterClassNegativeRange()
    {
        var matcher = TryCreateSectionNameMatcher("[!0-9]x").Value;
        Assert.AreEqual("^.*/[^0-9]x$", matcher.Regex.ToString());

        Assert.IsFalse(matcher.IsMatch("/0x"));
        Assert.IsFalse(matcher.IsMatch("/1x"));
        Assert.IsFalse(matcher.IsMatch("/9x"));
        Assert.IsTrue(matcher.IsMatch("/yx"));
        Assert.IsFalse(matcher.IsMatch("/00x"));
    }

    [MSBuildTestMethod]
    public void CharacterClassRangeAndChoice()
    {
        var matcher = TryCreateSectionNameMatcher("[ab0-9]x").Value;
        Assert.AreEqual("^.*/[ab0-9]x$", matcher.Regex.ToString());

        Assert.IsTrue(matcher.IsMatch("/ax"));
        Assert.IsTrue(matcher.IsMatch("/bx"));
        Assert.IsTrue(matcher.IsMatch("/0x"));
        Assert.IsTrue(matcher.IsMatch("/1x"));
        Assert.IsTrue(matcher.IsMatch("/9x"));
        Assert.IsFalse(matcher.IsMatch("/yx"));
        Assert.IsFalse(matcher.IsMatch("/0ax"));
    }

    [MSBuildTestMethod]
    public void CharacterClassOpenEnded()
    {
        var matcher = TryCreateSectionNameMatcher("[");
        Assert.IsNull(matcher);
    }

    [MSBuildTestMethod]
    public void CharacterClassEscapedOpenEnded()
    {
        var matcher = TryCreateSectionNameMatcher(@"[\]");
        Assert.IsNull(matcher);
    }

    [MSBuildTestMethod]
    public void CharacterClassEscapeAtEnd()
    {
        var matcher = TryCreateSectionNameMatcher(@"[\");
        Assert.IsNull(matcher);
    }

    [MSBuildTestMethod]
    public void CharacterClassOpenBracketInside()
    {
        var matcher = TryCreateSectionNameMatcher(@"[[a]bc").Value;

        Assert.IsTrue(matcher.IsMatch("/abc"));
        Assert.IsTrue(matcher.IsMatch("/[bc"));
        Assert.IsFalse(matcher.IsMatch("/ab"));
        Assert.IsFalse(matcher.IsMatch("/[b"));
        Assert.IsFalse(matcher.IsMatch("/bc"));
        Assert.IsFalse(matcher.IsMatch("/ac"));
        Assert.IsFalse(matcher.IsMatch("/[c"));

        Assert.AreEqual(@"^.*/[\[a]bc$", matcher.Regex.ToString());
    }

    [MSBuildTestMethod]
    public void CharacterClassStartingDash()
    {
        var matcher = TryCreateSectionNameMatcher(@"[-ac]bd").Value;

        Assert.IsTrue(matcher.IsMatch("/abd"));
        Assert.IsTrue(matcher.IsMatch("/cbd"));
        Assert.IsTrue(matcher.IsMatch("/-bd"));
        Assert.IsFalse(matcher.IsMatch("/bbd"));
        Assert.IsFalse(matcher.IsMatch("/-cd"));
        Assert.IsFalse(matcher.IsMatch("/bcd"));

        Assert.AreEqual(@"^.*/[-ac]bd$", matcher.Regex.ToString());
    }

    [MSBuildTestMethod]
    public void CharacterClassEndingDash()
    {
        var matcher = TryCreateSectionNameMatcher(@"[ac-]bd").Value;

        Assert.IsTrue(matcher.IsMatch("/abd"));
        Assert.IsTrue(matcher.IsMatch("/cbd"));
        Assert.IsTrue(matcher.IsMatch("/-bd"));
        Assert.IsFalse(matcher.IsMatch("/bbd"));
        Assert.IsFalse(matcher.IsMatch("/-cd"));
        Assert.IsFalse(matcher.IsMatch("/bcd"));

        Assert.AreEqual(@"^.*/[ac-]bd$", matcher.Regex.ToString());
    }

    [MSBuildTestMethod]
    public void CharacterClassEndBracketAfter()
    {
        var matcher = TryCreateSectionNameMatcher(@"[ab]]cd").Value;

        Assert.IsTrue(matcher.IsMatch("/a]cd"));
        Assert.IsTrue(matcher.IsMatch("/b]cd"));
        Assert.IsFalse(matcher.IsMatch("/acd"));
        Assert.IsFalse(matcher.IsMatch("/bcd"));
        Assert.IsFalse(matcher.IsMatch("/acd"));

        Assert.AreEqual(@"^.*/[ab]]cd$", matcher.Regex.ToString());
    }

    [MSBuildTestMethod]
    public void CharacterClassEscapeBackslash()
    {
        var matcher = TryCreateSectionNameMatcher(@"[ab\\]cd").Value;

        Assert.IsTrue(matcher.IsMatch("/acd"));
        Assert.IsTrue(matcher.IsMatch("/bcd"));
        Assert.IsTrue(matcher.IsMatch("/\\cd"));
        Assert.IsFalse(matcher.IsMatch("/dcd"));
        Assert.IsFalse(matcher.IsMatch("/\\\\cd"));
        Assert.IsFalse(matcher.IsMatch("/cd"));

        Assert.AreEqual(@"^.*/[ab\\]cd$", matcher.Regex.ToString());
    }

    [MSBuildTestMethod]
    public void EscapeOpenBracket()
    {
        var matcher = TryCreateSectionNameMatcher(@"ab\[cd").Value;

        Assert.IsTrue(matcher.IsMatch("/ab[cd"));
        Assert.IsFalse(matcher.IsMatch("/ab[[cd"));
        Assert.IsFalse(matcher.IsMatch("/abc"));
        Assert.IsFalse(matcher.IsMatch("/abd"));

        Assert.AreEqual(@"^.*/ab\[cd$", matcher.Regex.ToString());
    }
    #endregion

    #region Parsing Tests

    private static void SetEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T> comparer = null, string message = null)
    {
        var expectedSet = new HashSet<T>(expected, comparer);
        var result = expected.Count() == actual.Count() && expectedSet.SetEquals(actual);
        Assert.IsTrue(result, message);
    }

    private static void Equal<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        IEqualityComparer<T> comparer = null,
        string message = null)
    {
        if (expected == null)
        {
            Assert.IsNull(actual);
        }
        else
        {
            Assert.IsNotNull(actual);
        }

        if (SequenceEqual(expected, actual, comparer))
        {
            return;
        }

        Assert.Fail(message);
    }

    private static bool SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T> comparer = null)
    {
        if (ReferenceEquals(expected, actual))
        {
            return true;
        }

        var enumerator1 = expected.GetEnumerator();
        var enumerator2 = actual.GetEnumerator();

        while (true)
        {
            var hasNext1 = enumerator1.MoveNext();
            var hasNext2 = enumerator2.MoveNext();

            if (hasNext1 != hasNext2)
            {
                return false;
            }

            if (!hasNext1)
            {
                break;
            }

            var value1 = enumerator1.Current;
            var value2 = enumerator2.Current;

            if (!(comparer != null ? comparer.Equals(value1, value2) : AssertEqualityComparer<T>.Equals(value1, value2)))
            {
                return false;
            }
        }

        return true;
    }

    public static KeyValuePair<K, V> Create<K, V>(K key, V value)
    {
        return new KeyValuePair<K, V>(key, value);
    }

    [MSBuildTestMethod]
    public void SimpleCase()
    {
        var config = EditorConfigFile.Parse("""
root = true

# Comment1
# Comment2
##################################

my_global_prop = my_global_val

[*.cs]
my_prop = my_val
""");
        Assert.AreEqual("", config.GlobalSection.Name);
        var properties = config.GlobalSection.Properties;

        SetEqual(
            new[] { Create("my_global_prop", "my_global_val") ,
                    Create("root", "true") },
            properties);

        var namedSections = config.NamedSections;
        Assert.AreEqual("*.cs", namedSections[0].Name);
        SetEqual(
            new[] { Create("my_prop", "my_val") },
            namedSections[0].Properties);

        Assert.IsTrue(config.IsRoot);
    }


    [MSBuildTestMethod]
    // [WorkItem(52469, "https://github.com/dotnet/roslyn/issues/52469")]
    public void ConfigWithEscapedValues()
    {
        var config = EditorConfigFile.Parse(@"is_global = true

[c:/\{f\*i\?le1\}.cs]
build_metadata.Compile.ToRetrieve = abc123

[c:/f\,ile\#2.cs]
build_metadata.Compile.ToRetrieve = def456

[c:/f\;i\!le\[3\].cs]
build_metadata.Compile.ToRetrieve = ghi789
");

        var namedSections = config.NamedSections;
        Assert.AreEqual("c:/\\{f\\*i\\?le1\\}.cs", namedSections[0].Name);
        Equal(
            new[] { Create("build_metadata.compile.toretrieve", "abc123") },
            namedSections[0].Properties);

        Assert.AreEqual("c:/f\\,ile\\#2.cs", namedSections[1].Name);
        Equal(
            new[] { Create("build_metadata.compile.toretrieve", "def456") },
            namedSections[1].Properties);

        Assert.AreEqual("c:/f\\;i\\!le\\[3\\].cs", namedSections[2].Name);
        Equal(
            new[] { Create("build_metadata.compile.toretrieve", "ghi789") },
            namedSections[2].Properties);
    }

    /*
    [MSBuildTestMethod]
    [WorkItem(52469, "https://github.com/dotnet/roslyn/issues/52469")]
    public void CanGetSectionsWithSpecialCharacters()
    {
        var config = ParseConfigFile(@"is_global = true

[/home/foo/src/\{releaseid\}.cs]
build_metadata.Compile.ToRetrieve = abc123

[/home/foo/src/Pages/\#foo/HomePage.cs]
build_metadata.Compile.ToRetrieve = def456
");

        var set = CheckConfigSet.Create(ImmutableArray.Create(config));

        var sectionOptions = set.GetOptionsForSourcePath("/home/foo/src/{releaseid}.cs");
        Assert.AreEqual("abc123", sectionOptions.CheckOptions["build_metadata.compile.toretrieve"]);

        sectionOptions = set.GetOptionsForSourcePath("/home/foo/src/Pages/#foo/HomePage.cs");
        Assert.AreEqual("def456", sectionOptions.CheckOptions["build_metadata.compile.toretrieve"]);
    }*/

    [MSBuildTestMethod]
    public void MissingClosingBracket()
    {
        var config = EditorConfigFile.Parse(@"
[*.cs
my_prop = my_val");
        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my_prop", "my_val") },
            properties);

        Assert.AreEqual(0, config.NamedSections.Length);
    }


    [MSBuildTestMethod]
    public void EmptySection()
    {
        var config = EditorConfigFile.Parse(@"
[]
my_prop = my_val");

        var properties = config.GlobalSection.Properties;
        SetEqual(new[] { Create("my_prop", "my_val") }, properties);
        Assert.AreEqual(0, config.NamedSections.Length);
    }


    [MSBuildTestMethod]
    public void CaseInsensitivePropKey()
    {
        var config = EditorConfigFile.Parse(@"
my_PROP = my_VAL");
        var properties = config.GlobalSection.Properties;

        Assert.IsTrue(properties.TryGetValue("my_PrOp", out var val));
        Assert.AreEqual("my_VAL", val);
        Assert.AreEqual("my_prop", properties.Keys.Single());
    }

    // there is no reversed keys support for msbuild
    /*[MSBuildTestMethod]
    public void NonReservedKeyPreservedCaseVal()
    {
        var config = ParseConfigFile(string.Join(Environment.NewLine,
            CheckConfig.ReservedKeys.Select(k => "MY_" + k + " = MY_VAL")));
        AssertEx.SetEqual(
            CheckConfig.ReservedKeys.Select(k => KeyValuePair.Create("my_" + k, "MY_VAL")).ToList(),
            config.GlobalSection.Properties);
    }*/


    [MSBuildTestMethod]
    public void DuplicateKeys()
    {
        var config = EditorConfigFile.Parse(@"
my_prop = my_val
my_prop = my_other_val");

        var properties = config.GlobalSection.Properties;
        SetEqual(new[] { Create("my_prop", "my_other_val") }, properties);
    }


    [MSBuildTestMethod]
    public void DuplicateKeysCasing()
    {
        var config = EditorConfigFile.Parse(@"
my_prop = my_val
my_PROP = my_other_val");

        var properties = config.GlobalSection.Properties;
        SetEqual(new[] { Create("my_prop", "my_other_val") }, properties);
    }


    [MSBuildTestMethod]
    public void MissingKey()
    {
        var config = EditorConfigFile.Parse(@"
= my_val1
my_prop = my_val2");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my_prop", "my_val2") },
            properties);
    }



    [MSBuildTestMethod]
    public void MissingVal()
    {
        var config = EditorConfigFile.Parse(@"
my_prop1 =
my_prop2 = my_val");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my_prop1", ""),
                    Create("my_prop2", "my_val") },
            properties);
    }


    [MSBuildTestMethod]
    public void SpacesInProperties()
    {
        var config = EditorConfigFile.Parse(@"
my prop1 = my_val1
my_prop2 = my val2");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my_prop2", "my val2") },
            properties);
    }


    [MSBuildTestMethod]
    public void EndOfLineComments()
    {
        var config = EditorConfigFile.Parse(@"
my_prop2 = my val2 # Comment");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my_prop2", "my val2") },
            properties);
    }

    [MSBuildTestMethod]
    public void SymbolsStartKeys()
    {
        var config = EditorConfigFile.Parse(@"
@!$abc = my_val1
@!$\# = my_val2");

        var properties = config.GlobalSection.Properties;
        Assert.AreEqual(0, properties.Count);
    }


    [MSBuildTestMethod]
    public void EqualsAndColon()
    {
        var config = EditorConfigFile.Parse(@"
my:key1 = my_val
my_key2 = my:val");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my", "key1 = my_val"),
                    Create("my_key2", "my:val")},
            properties);
    }

    [MSBuildTestMethod]
    public void SymbolsInProperties()
    {
        var config = EditorConfigFile.Parse(@"
my@key1 = my_val
my_key2 = my@val");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("my_key2", "my@val") },
            properties);
    }

    [MSBuildTestMethod]
    public void LongLines()
    {
        // This example is described in the Python ConfigParser as allowing
        // line continuation via the RFC 822 specification, section 3.1.1
        // LONG HEADER FIELDS. The VS parser does not accept this as a
        // valid parse for an editorconfig file. We follow similarly.
        var config = EditorConfigFile.Parse(@"
long: this value continues
   in the next line");

        var properties = config.GlobalSection.Properties;
        SetEqual(
            new[] { Create("long", "this value continues") },
            properties);
    }


    [MSBuildTestMethod]
    public void CaseInsensitiveRoot()
    {
        var config = EditorConfigFile.Parse(@"
RoOt = TruE");
        Assert.IsTrue(config.IsRoot);
    }


    /*
    Reserved values are not supported at the moment
    [MSBuildTestMethod]
    public void ReservedValues()
    {
        int index = 0;
        var config = ParseConfigFile(string.Join(Environment.NewLine,
            CheckConfig.ReservedValues.Select(v => "MY_KEY" + (index++) + " = " + v.ToUpperInvariant())));
        index = 0;
        AssertEx.SetEqual(
            CheckConfig.ReservedValues.Select(v => KeyValuePair.Create("my_key" + (index++), v)).ToList(),
            config.GlobalSection.Properties);
    }
    */

    /*
    [MSBuildTestMethod]
    public void ReservedKeys()
    {
        var config = ParseConfigFile(string.Join(Environment.NewLine,
            CheckConfig.ReservedKeys.Select(k => k + " = MY_VAL")));
        AssertEx.SetEqual(
            CheckConfig.ReservedKeys.Select(k => KeyValuePair.Create(k, "my_val")).ToList(),
            config.GlobalSection.Properties);
    }
    */
    #endregion
}
