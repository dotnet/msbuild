using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using Microsoft.Build.Shared;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Microsoft.Build.Engine.UnitTests
{
    public class FileUtilitiesRegex_Tests
    {
        private string _directoryStart = new string(MSBuildConstants.BackslashChar[0], 2);
        private string _altDirectoryStart = new string(MSBuildConstants.ForwardSlash[0], 2);

        //below are the legacy regex used before explcitly checking these patterns to reduce allocations
        
        // regular expression used to match file-specs comprising exactly "<drive letter>:" (with no trailing characters)
        internal static readonly Regex DrivePattern = new Regex(@"^[A-Za-z]:$", RegexOptions.Compiled);

        // regular expression used to match file-specs beginning with "<drive letter>:"
        internal static readonly Regex StartWithDrivePattern = new Regex(@"^[A-Za-z]:", RegexOptions.Compiled);

        private static readonly string s_baseUncPattern = string.Format(
            CultureInfo.InvariantCulture,
            @"^[\{0}\{1}][\{0}\{1}][^\{0}\{1}]+[\{0}\{1}][^\{0}\{1}]+",
            '\\', '/');

        // regular expression used to match UNC paths beginning with "\\<server>\<share>"
        internal static readonly Regex StartsWithUncPattern = new Regex(s_baseUncPattern, RegexOptions.Compiled);

        // regular expression used to match UNC paths comprising exactly "\\<server>\<share>"
        internal static readonly Regex UncPattern =
            new Regex(
                string.Format(CultureInfo.InvariantCulture, @"{0}$", s_baseUncPattern),
                RegexOptions.Compiled);

        [Fact]
        public void DrivePatternIsMatchAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                DrivePattern.IsMatch(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                DrivePattern.IsMatch(s).ShouldBeTrue();
            }
        }

        [Fact]
        public void IsDrivePatternAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.IsDrivePattern(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.IsDrivePattern(s).ShouldBeTrue();
            }
        }

        [Fact]
        public void StartWithDrivePatternIsMatchAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                StartWithDrivePattern.IsMatch(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                StartWithDrivePattern.IsMatch(s).ShouldBeTrue();
            }
        }

        [Fact]
        public void DoesStartWithDrivePatternAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.StartsWithDrivePattern(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.StartsWithDrivePattern(s).ShouldBeTrue();
            }
        }

        [Theory]
        [InlineData(" C:")]
        [InlineData(" :")]
        [InlineData("CC:")]
        [InlineData("::")]
        [InlineData("x\\")]
        [InlineData(":/")]
        [InlineData("  ")]
        [InlineData("")]
        public void DrivePatternInvalidFormat_LegacyRegex(string value)
        {
            DrivePattern.IsMatch(value).ShouldBe(false);
            StartWithDrivePattern.IsMatch(value).ShouldBe(false);
        }

        [Theory]
        [InlineData(" C:")]
        [InlineData(" :")]
        [InlineData("CC:")]
        [InlineData("::")]
        [InlineData("x\\")]
        [InlineData(":/")]
        [InlineData("  ")]
        [InlineData("")]
        public void DrivePatternInvalidFormat(string value)
        {
            FileUtilitiesRegex.StartsWithDrivePattern(value).ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern(value).ShouldBe(false);
        }

        [Theory]
        [InlineData("C: ")]
        [InlineData("C:/")]
        [InlineData("Z:\\")]
        [InlineData("b:a\\q/")]
        public void StartWithDrivePatternInvalidPatternValidStart_LegacyRegex(string value)
        {
            StartWithDrivePattern.IsMatch(value).ShouldBeTrue();
        }

        [Theory]
        [InlineData("C: ")]
        [InlineData("C:/")]
        [InlineData("Z:\\")]
        [InlineData("b:a\\q/")]
        public void StartWithDrivePatternInvalidPatternValidStart(string value)
        {
            FileUtilitiesRegex.StartsWithDrivePattern(value).ShouldBeTrue();
        }

        [Fact]
        public void UncPatternMultiFolderDirectory_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server\\path\\test", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBe(false);
            UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void UncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartWithUncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartWithUncPatternMultiFolderDirectory_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(13);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(13);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternMultiFolderDirectory_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);
        }

        [Fact]
        public void UncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void UncPatternExactDirectory_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBeTrue();
            UncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }


        [Fact]
        public void StartWithUncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartWithUncPatternExactDirectory_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(13);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(13);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternExactDirectory_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);
        }

        [Fact]
        public void UncPatternMixedSlashes_LegacyRegex()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBeTrue();
            UncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void UncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartWithUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartWithUncPatternMixedSlashes_LegacyRegex()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternMixedSlashes_LegacyRegex()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(9);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(9);
        }

        [Fact]
        public void UncPatternTrailingSlash_LegacyRegex()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBe(false);
            UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void UncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);

        }

        [Fact]
        public void StartWithUncPatternTrailingSlash_LegacyRegex()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();

        }

        [Fact]
        public void StartWithUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternTrailingSlash_LegacyRegex()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(9);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(9);
        }

        [Fact]
        public void UncPatternLessThanMinimum_LegacyRegex()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBe(false);
            UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void UncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartWithUncPatternLessThanMinimum_LegacyRegex()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartWithUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternLessThanMinimum_LegacyRegex()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeFalse();

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeFalse();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(-1);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(-1);
        }

        [Fact]
        public void UncPatternNoShare_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBe(false);
            UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternNoShare()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartWithUncPatternNoShare_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartWithUncPatternNoShare()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternNoShare_LegacyRegex()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeFalse();

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeFalse();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternNoShare()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(-1);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(-1);
        }

        [Fact]
        public void UncPatternEmptyString_LegacyRegex()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            UncPattern.IsMatch(winDirectory).ShouldBe(false);
            UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void UncPatternEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartWithUncPatternEmptyString_LegacyRegex()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartsWithUncPatternEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void MatchLengthStartWithUncPatternEmptyString_LegacyRegex()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            var match = StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeFalse();

            match = StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeFalse();
        }

        [Fact]
        public void MatchLengthStartWithUncPatternEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPatternMatchLength(winDirectory).ShouldBe(-1);
            FileUtilitiesRegex.StartsWithUncPatternMatchLength(unixDirectory).ShouldBe(-1);
        }
    }
}
