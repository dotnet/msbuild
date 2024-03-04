// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.UnitTests;
using Xunit;
using static Microsoft.Build.BuildCop.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;

#nullable disable

namespace Microsoft.Build.Analyzers.UnitTests
{
    public class EditorConfig_Tests
    {
        // Section Matchin Test cases: https://github.com/dotnet/roslyn/blob/ba163e712b01358a217065eec8a4a82f94a7efd5/src/Compilers/Core/CodeAnalysisTest/Analyzers/AnalyzerConfigTests.cs#L337
        #region Section Matching Tests
        [Fact]
        public void SimpleNameMatch()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("abc").Value;
            Assert.Equal("^.*/abc$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc"));
            Assert.False(matcher.IsMatch("/aabc"));
            Assert.False(matcher.IsMatch("/ abc"));
            Assert.False(matcher.IsMatch("/cabc"));
        }

        [Fact]
        public void StarOnlyMatch()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("*").Value;
            Assert.Equal("^.*/[^/]*$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc"));
            Assert.True(matcher.IsMatch("/123"));
            Assert.True(matcher.IsMatch("/abc/123"));
        }

        [Fact]
        public void StarNameMatch()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("*.cs").Value;
            Assert.Equal("^.*/[^/]*\\.cs$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc.cs"));
            Assert.True(matcher.IsMatch("/123.cs"));
            Assert.True(matcher.IsMatch("/dir/subpath.cs"));
            // Only '/' is defined as a directory separator, so the caller
            // is responsible for converting any other machine directory
            // separators to '/' before matching
            Assert.True(matcher.IsMatch("/dir\\subpath.cs"));

            Assert.False(matcher.IsMatch("/abc.vb"));
        }

        [Fact]
        public void StarStarNameMatch()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("**.cs").Value;
            Assert.Equal("^.*/.*\\.cs$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc.cs"));
            Assert.True(matcher.IsMatch("/dir/subpath.cs"));
        }

        [Fact]
        public void EscapeDot()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("...").Value;
            Assert.Equal("^.*/\\.\\.\\.$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/..."));
            Assert.True(matcher.IsMatch("/subdir/..."));
            Assert.False(matcher.IsMatch("/aaa"));
            Assert.False(matcher.IsMatch("/???"));
            Assert.False(matcher.IsMatch("/abc"));
        }

        [Fact]
        public void EndBackslashMatch()
        {
            SectionNameMatcher? matcher = TryCreateSectionNameMatcher("abc\\");
            Assert.Null(matcher);
        }

        [Fact]
        public void QuestionMatch()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab?def").Value;
            Assert.Equal("^.*/ab.def$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abcdef"));
            Assert.True(matcher.IsMatch("/ab?def"));
            Assert.True(matcher.IsMatch("/abzdef"));
            Assert.True(matcher.IsMatch("/ab/def"));
            Assert.True(matcher.IsMatch("/ab\\def"));
        }

        [Fact]
        public void LiteralBackslash()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab\\\\c").Value;
            Assert.Equal("^.*/ab\\\\c$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/ab\\c"));
            Assert.False(matcher.IsMatch("/ab/c"));
            Assert.False(matcher.IsMatch("/ab\\\\c"));
        }

        [Fact]
        public void LiteralStars()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("\\***\\*\\**").Value;
            Assert.Equal("^.*/\\*.*\\*\\*[^/]*$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/*ab/cd**efg*"));
            Assert.False(matcher.IsMatch("/ab/cd**efg*"));
            Assert.False(matcher.IsMatch("/*ab/cd*efg*"));
            Assert.False(matcher.IsMatch("/*ab/cd**ef/gh"));
        }

        [Fact]
        public void LiteralQuestions()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("\\??\\?*\\??").Value;
            Assert.Equal("^.*/\\?.\\?[^/]*\\?.$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/?a?cde?f"));
            Assert.True(matcher.IsMatch("/???????f"));
            Assert.False(matcher.IsMatch("/aaaaaaaa"));
            Assert.False(matcher.IsMatch("/aa?cde?f"));
            Assert.False(matcher.IsMatch("/?a?cdexf"));
            Assert.False(matcher.IsMatch("/?axcde?f"));
        }

        [Fact]
        public void LiteralBraces()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("abc\\{\\}def").Value;
            Assert.Equal(@"^.*/abc\{}def$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc{}def"));
            Assert.True(matcher.IsMatch("/subdir/abc{}def"));
            Assert.False(matcher.IsMatch("/abcdef"));
            Assert.False(matcher.IsMatch("/abc}{def"));
        }

        [Fact]
        public void LiteralComma()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("abc\\,def").Value;
            Assert.Equal("^.*/abc,def$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc,def"));
            Assert.True(matcher.IsMatch("/subdir/abc,def"));
            Assert.False(matcher.IsMatch("/abcdef"));
            Assert.False(matcher.IsMatch("/abc\\,def"));
            Assert.False(matcher.IsMatch("/abc`def"));
        }

        [Fact]
        public void SimpleChoice()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("*.{cs,vb,fs}").Value;
            Assert.Equal("^.*/[^/]*\\.(?:cs|vb|fs)$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc.cs"));
            Assert.True(matcher.IsMatch("/abc.vb"));
            Assert.True(matcher.IsMatch("/abc.fs"));
            Assert.True(matcher.IsMatch("/subdir/abc.cs"));
            Assert.True(matcher.IsMatch("/subdir/abc.vb"));
            Assert.True(matcher.IsMatch("/subdir/abc.fs"));

            Assert.False(matcher.IsMatch("/abcxcs"));
            Assert.False(matcher.IsMatch("/abcxvb"));
            Assert.False(matcher.IsMatch("/abcxfs"));
            Assert.False(matcher.IsMatch("/subdir/abcxcs"));
            Assert.False(matcher.IsMatch("/subdir/abcxcb"));
            Assert.False(matcher.IsMatch("/subdir/abcxcs"));
        }

        [Fact]
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
            Assert.Equal("^/(?:[^/]*\\.cs|subdir/test\\.vb)$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/test.cs"));
            Assert.True(matcher.IsMatch("/subdir/test.vb"));

            Assert.False(matcher.IsMatch("/subdir/test.cs"));
            Assert.False(matcher.IsMatch("/subdir/subdir/test.vb"));
            Assert.False(matcher.IsMatch("/test.vb"));
        }

        [Fact]
        public void EmptyChoice()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("{}").Value;
            Assert.Equal("^.*/(?:)$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/"));
            Assert.True(matcher.IsMatch("/subdir/"));
            Assert.False(matcher.IsMatch("/."));
            Assert.False(matcher.IsMatch("/anything"));
        }

        [Fact]
        public void SingleChoice()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("{*.cs}").Value;
            Assert.Equal("^.*/(?:[^/]*\\.cs)$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/test.cs"));
            Assert.True(matcher.IsMatch("/subdir/test.cs"));
            Assert.False(matcher.IsMatch("test.vb"));
            Assert.False(matcher.IsMatch("testxcs"));
        }

        [Fact]
        public void UnmatchedBraces()
        {
            SectionNameMatcher? matcher = TryCreateSectionNameMatcher("{{{{}}");
            Assert.Null(matcher);
        }

        [Fact]
        public void CommaOutsideBraces()
        {
            SectionNameMatcher? matcher = TryCreateSectionNameMatcher("abc,def");
            Assert.Null(matcher);
        }

        [Fact]
        public void RecursiveChoice()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("{test{.cs,.vb},other.{a{bb,cc}}}").Value;
            Assert.Equal("^.*/(?:test(?:\\.cs|\\.vb)|other\\.(?:a(?:bb|cc)))$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/test.cs"));
            Assert.True(matcher.IsMatch("/test.vb"));
            Assert.True(matcher.IsMatch("/subdir/test.cs"));
            Assert.True(matcher.IsMatch("/subdir/test.vb"));
            Assert.True(matcher.IsMatch("/other.abb"));
            Assert.True(matcher.IsMatch("/other.acc"));

            Assert.False(matcher.IsMatch("/test.fs"));
            Assert.False(matcher.IsMatch("/other.bbb"));
            Assert.False(matcher.IsMatch("/other.ccc"));
            Assert.False(matcher.IsMatch("/subdir/other.bbb"));
            Assert.False(matcher.IsMatch("/subdir/other.ccc"));
        }

        [Fact]
        public void DashChoice()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab{-}cd{-,}ef").Value;
            Assert.Equal("^.*/ab(?:-)cd(?:-|)ef$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/ab-cd-ef"));
            Assert.True(matcher.IsMatch("/ab-cdef"));

            Assert.False(matcher.IsMatch("/abcdef"));
            Assert.False(matcher.IsMatch("/ab--cd-ef"));
            Assert.False(matcher.IsMatch("/ab--cd--ef"));
        }

        [Fact]
        public void MiddleMatch()
        {
            SectionNameMatcher matcher = TryCreateSectionNameMatcher("ab{cs,vb,fs}cd").Value;
            Assert.Equal("^.*/ab(?:cs|vb|fs)cd$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abcscd"));
            Assert.True(matcher.IsMatch("/abvbcd"));
            Assert.True(matcher.IsMatch("/abfscd"));

            Assert.False(matcher.IsMatch("/abcs"));
            Assert.False(matcher.IsMatch("/abcd"));
            Assert.False(matcher.IsMatch("/vbcd"));
        }

        private static IEnumerable<(string, string)> RangeAndInverse(string s1, string s2)
        {
            yield return (s1, s2);
            yield return (s2, s1);
        }

        [Fact]
        public void NumberMatch()
        {
            foreach (var (i1, i2) in RangeAndInverse("0", "10"))
            {
                var matcher = TryCreateSectionNameMatcher($"{{{i1}..{i2}}}").Value;

                Assert.True(matcher.IsMatch("/0"));
                Assert.True(matcher.IsMatch("/10"));
                Assert.True(matcher.IsMatch("/5"));
                Assert.True(matcher.IsMatch("/000005"));
                Assert.False(matcher.IsMatch("/-1"));
                Assert.False(matcher.IsMatch("/-00000001"));
                Assert.False(matcher.IsMatch("/11"));
            }
        }

        [Fact]
        public void NumberMatchNegativeRange()
        {
            foreach (var (i1, i2) in RangeAndInverse("-10", "0"))
            {
                var matcher = TryCreateSectionNameMatcher($"{{{i1}..{i2}}}").Value;

                Assert.True(matcher.IsMatch("/0"));
                Assert.True(matcher.IsMatch("/-10"));
                Assert.True(matcher.IsMatch("/-5"));
                Assert.False(matcher.IsMatch("/1"));
                Assert.False(matcher.IsMatch("/-11"));
                Assert.False(matcher.IsMatch("/--0"));
            }
        }

        [Fact]
        public void NumberMatchNegToPos()
        {
            foreach (var (i1, i2) in RangeAndInverse("-10", "10"))
            {
                var matcher = TryCreateSectionNameMatcher($"{{{i1}..{i2}}}").Value;

                Assert.True(matcher.IsMatch("/0"));
                Assert.True(matcher.IsMatch("/-5"));
                Assert.True(matcher.IsMatch("/5"));
                Assert.True(matcher.IsMatch("/-10"));
                Assert.True(matcher.IsMatch("/10"));
                Assert.False(matcher.IsMatch("/-11"));
                Assert.False(matcher.IsMatch("/11"));
                Assert.False(matcher.IsMatch("/--0"));
            }
        }

        [Fact]
        public void MultipleNumberRanges()
        {
            foreach (var matchString in new[] { "a{-10..0}b{0..10}", "a{0..-10}b{10..0}" })
            {
                var matcher = TryCreateSectionNameMatcher(matchString).Value;

                Assert.True(matcher.IsMatch("/a0b0"));
                Assert.True(matcher.IsMatch("/a-5b0"));
                Assert.True(matcher.IsMatch("/a-5b5"));
                Assert.True(matcher.IsMatch("/a-5b10"));
                Assert.True(matcher.IsMatch("/a-10b10"));
                Assert.True(matcher.IsMatch("/a-10b0"));
                Assert.True(matcher.IsMatch("/a-0b0"));
                Assert.True(matcher.IsMatch("/a-0b-0"));

                Assert.False(matcher.IsMatch("/a-11b10"));
                Assert.False(matcher.IsMatch("/a-11b10"));
                Assert.False(matcher.IsMatch("/a-10b11"));
            }
        }

        [Fact]
        public void BadNumberRanges()
        {
            var matcherOpt = TryCreateSectionNameMatcher("{0..");

            Assert.Null(matcherOpt);

            var matcher = TryCreateSectionNameMatcher("{0..}").Value;

            Assert.True(matcher.IsMatch("/0.."));
            Assert.False(matcher.IsMatch("/0"));
            Assert.False(matcher.IsMatch("/0."));
            Assert.False(matcher.IsMatch("/0abc"));

            matcher = TryCreateSectionNameMatcher("{0..A}").Value;
            Assert.True(matcher.IsMatch("/0..A"));
            Assert.False(matcher.IsMatch("/0"));
            Assert.False(matcher.IsMatch("/0abc"));

            // The reference implementation uses atoi here so we can presume
            // numbers out of range of Int32 are not well supported
            matcherOpt = TryCreateSectionNameMatcher($"{{0..{UInt32.MaxValue}}}");

            Assert.Null(matcherOpt);
        }

        [Fact]
        public void CharacterClassSimple()
        {
            var matcher = TryCreateSectionNameMatcher("*.[cf]s").Value;
            Assert.Equal(@"^.*/[^/]*\.[cf]s$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc.cs"));
            Assert.True(matcher.IsMatch("/abc.fs"));
            Assert.False(matcher.IsMatch("/abc.vs"));
        }

        [Fact]
        public void CharacterClassNegative()
        {
            var matcher = TryCreateSectionNameMatcher("*.[!cf]s").Value;
            Assert.Equal(@"^.*/[^/]*\.[^cf]s$", matcher.Regex.ToString());

            Assert.False(matcher.IsMatch("/abc.cs"));
            Assert.False(matcher.IsMatch("/abc.fs"));
            Assert.True(matcher.IsMatch("/abc.vs"));
            Assert.True(matcher.IsMatch("/abc.xs"));
            Assert.False(matcher.IsMatch("/abc.vxs"));
        }

        [Fact]
        public void CharacterClassCaret()
        {
            var matcher = TryCreateSectionNameMatcher("*.[^cf]s").Value;
            Assert.Equal(@"^.*/[^/]*\.[\^cf]s$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/abc.cs"));
            Assert.True(matcher.IsMatch("/abc.fs"));
            Assert.True(matcher.IsMatch("/abc.^s"));
            Assert.False(matcher.IsMatch("/abc.vs"));
            Assert.False(matcher.IsMatch("/abc.xs"));
            Assert.False(matcher.IsMatch("/abc.vxs"));
        }

        [Fact]
        public void CharacterClassRange()
        {
            var matcher = TryCreateSectionNameMatcher("[0-9]x").Value;
            Assert.Equal("^.*/[0-9]x$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/0x"));
            Assert.True(matcher.IsMatch("/1x"));
            Assert.True(matcher.IsMatch("/9x"));
            Assert.False(matcher.IsMatch("/yx"));
            Assert.False(matcher.IsMatch("/00x"));
        }

        [Fact]
        public void CharacterClassNegativeRange()
        {
            var matcher = TryCreateSectionNameMatcher("[!0-9]x").Value;
            Assert.Equal("^.*/[^0-9]x$", matcher.Regex.ToString());

            Assert.False(matcher.IsMatch("/0x"));
            Assert.False(matcher.IsMatch("/1x"));
            Assert.False(matcher.IsMatch("/9x"));
            Assert.True(matcher.IsMatch("/yx"));
            Assert.False(matcher.IsMatch("/00x"));
        }

        [Fact]
        public void CharacterClassRangeAndChoice()
        {
            var matcher = TryCreateSectionNameMatcher("[ab0-9]x").Value;
            Assert.Equal("^.*/[ab0-9]x$", matcher.Regex.ToString());

            Assert.True(matcher.IsMatch("/ax"));
            Assert.True(matcher.IsMatch("/bx"));
            Assert.True(matcher.IsMatch("/0x"));
            Assert.True(matcher.IsMatch("/1x"));
            Assert.True(matcher.IsMatch("/9x"));
            Assert.False(matcher.IsMatch("/yx"));
            Assert.False(matcher.IsMatch("/0ax"));
        }

        [Fact]
        public void CharacterClassOpenEnded()
        {
            var matcher = TryCreateSectionNameMatcher("[");
            Assert.Null(matcher);
        }

        [Fact]
        public void CharacterClassEscapedOpenEnded()
        {
            var matcher = TryCreateSectionNameMatcher(@"[\]");
            Assert.Null(matcher);
        }

        [Fact]
        public void CharacterClassEscapeAtEnd()
        {
            var matcher = TryCreateSectionNameMatcher(@"[\");
            Assert.Null(matcher);
        }

        [Fact]
        public void CharacterClassOpenBracketInside()
        {
            var matcher = TryCreateSectionNameMatcher(@"[[a]bc").Value;

            Assert.True(matcher.IsMatch("/abc"));
            Assert.True(matcher.IsMatch("/[bc"));
            Assert.False(matcher.IsMatch("/ab"));
            Assert.False(matcher.IsMatch("/[b"));
            Assert.False(matcher.IsMatch("/bc"));
            Assert.False(matcher.IsMatch("/ac"));
            Assert.False(matcher.IsMatch("/[c"));

            Assert.Equal(@"^.*/[\[a]bc$", matcher.Regex.ToString());
        }

        [Fact]
        public void CharacterClassStartingDash()
        {
            var matcher = TryCreateSectionNameMatcher(@"[-ac]bd").Value;

            Assert.True(matcher.IsMatch("/abd"));
            Assert.True(matcher.IsMatch("/cbd"));
            Assert.True(matcher.IsMatch("/-bd"));
            Assert.False(matcher.IsMatch("/bbd"));
            Assert.False(matcher.IsMatch("/-cd"));
            Assert.False(matcher.IsMatch("/bcd"));

            Assert.Equal(@"^.*/[-ac]bd$", matcher.Regex.ToString());
        }

        [Fact]
        public void CharacterClassEndingDash()
        {
            var matcher = TryCreateSectionNameMatcher(@"[ac-]bd").Value;

            Assert.True(matcher.IsMatch("/abd"));
            Assert.True(matcher.IsMatch("/cbd"));
            Assert.True(matcher.IsMatch("/-bd"));
            Assert.False(matcher.IsMatch("/bbd"));
            Assert.False(matcher.IsMatch("/-cd"));
            Assert.False(matcher.IsMatch("/bcd"));

            Assert.Equal(@"^.*/[ac-]bd$", matcher.Regex.ToString());
        }

        [Fact]
        public void CharacterClassEndBracketAfter()
        {
            var matcher = TryCreateSectionNameMatcher(@"[ab]]cd").Value;

            Assert.True(matcher.IsMatch("/a]cd"));
            Assert.True(matcher.IsMatch("/b]cd"));
            Assert.False(matcher.IsMatch("/acd"));
            Assert.False(matcher.IsMatch("/bcd"));
            Assert.False(matcher.IsMatch("/acd"));

            Assert.Equal(@"^.*/[ab]]cd$", matcher.Regex.ToString());
        }

        [Fact]
        public void CharacterClassEscapeBackslash()
        {
            var matcher = TryCreateSectionNameMatcher(@"[ab\\]cd").Value;

            Assert.True(matcher.IsMatch("/acd"));
            Assert.True(matcher.IsMatch("/bcd"));
            Assert.True(matcher.IsMatch("/\\cd"));
            Assert.False(matcher.IsMatch("/dcd"));
            Assert.False(matcher.IsMatch("/\\\\cd"));
            Assert.False(matcher.IsMatch("/cd"));

            Assert.Equal(@"^.*/[ab\\]cd$", matcher.Regex.ToString());
        }

        [Fact]
        public void EscapeOpenBracket()
        {
            var matcher = TryCreateSectionNameMatcher(@"ab\[cd").Value;

            Assert.True(matcher.IsMatch("/ab[cd"));
            Assert.False(matcher.IsMatch("/ab[[cd"));
            Assert.False(matcher.IsMatch("/abc"));
            Assert.False(matcher.IsMatch("/abd"));

            Assert.Equal(@"^.*/ab\[cd$", matcher.Regex.ToString());
        }
        #endregion
    }
}
