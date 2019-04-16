using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using Microsoft.Build.Shared;
using System.IO;

namespace Microsoft.Build.Engine.UnitTests
{
    public class FileUtilitiesRegex_Tests
    {
        private string _directoryStart = new string(MSBuildConstants.BackslashChar[0], 2);
        private string _altDirectoryStart = new string(MSBuildConstants.ForwardSlash[0], 2);

        [Fact]
        public void DrivePatternIsMatchAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.DrivePattern.IsMatch(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.DrivePattern.IsMatch(s).ShouldBeTrue();
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
                FileUtilitiesRegex.StartWithDrivePattern.IsMatch(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.StartWithDrivePattern.IsMatch(s).ShouldBeTrue();
            }
        }

        [Fact]
        public void DoesStartWithDrivePatternAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.DoesStartWithDrivePattern(s).ShouldBeTrue();
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.DoesStartWithDrivePattern(s).ShouldBeTrue();
            }
        }

        /// <summary>
        /// Tests DrivePattern regex using various invalid drive patterns.
        /// </summary>
        [Theory]
        [InlineData("C: ")]
        [InlineData(" C:")]
        [InlineData(" :")]
        [InlineData("CC:")]
        [InlineData("::")]
        [InlineData("C:/")]
        [InlineData("C:\\")]
        [InlineData("C\\")]
        [InlineData(":/")]
        [InlineData("  ")]
        [InlineData("")]
        public void DrivePatternIsMatchInvalidFormat(string value)
        {
            FileUtilitiesRegex.DrivePattern.IsMatch(value).ShouldBe(false);
        }


        /// <summary>
        /// Tests DrivePattern without regex using various invalid drive patterns.
        /// </summary>
        [Theory]
        [InlineData("C: ")]
        [InlineData(" C:")]
        [InlineData(" :")]
        [InlineData("CC:")]
        [InlineData("::")]
        [InlineData("C:/")]
        [InlineData("C:\\")]
        [InlineData("C\\")]
        [InlineData(":/")]
        [InlineData("  ")]
        [InlineData("")]
        public void IsDrivePatternInvalidFormat(string value)
        {
            FileUtilitiesRegex.IsDrivePattern(value).ShouldBe(false);
        }

        /// <summary>
        /// Tests DrivePattern regex using varopis invalid drive patterns.
        /// </summary>
        [Theory]
        [InlineData(" C:")]
        [InlineData(" :")]
        [InlineData("CC:")]
        [InlineData("::")]
        [InlineData("x\\")]
        [InlineData(":/")]
        [InlineData("  ")]
        [InlineData("")]
        public void StartWithDriveIsMatchPatternInvalidFormat(string value)
        {
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch(value).ShouldBe(false);
        }

        /// <summary>
        /// Tests DrivePattern regex using various invalid drive patterns.
        /// </summary>
        [Theory]
        [InlineData(" C:")]
        [InlineData(" :")]
        [InlineData("CC:")]
        [InlineData("::")]
        [InlineData("x\\")]
        [InlineData(":/")]
        [InlineData("  ")]
        [InlineData("")]
        public void DoesStartWithDrivePatternInvalidFormat(string value)
        {
            FileUtilitiesRegex.DoesStartWithDrivePattern(value).ShouldBe(false);
        }

        [Theory]
        [InlineData("C: ")]
        [InlineData("C:/")]
        [InlineData("Z:\\")]
        [InlineData("b:a\\q/")]
        public void StartWithDrivePatternIsMatchInvalidPatternValidStart(string value)
        {
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch(value).ShouldBeTrue();
        }

        [Theory]
        [InlineData("C: ")]
        [InlineData("C:/")]
        [InlineData("Z:\\")]
        [InlineData("b:a\\q/")]
        public void DoesStartWithDrivePatternInvalidPatternValidStart(string value)
        {
            FileUtilitiesRegex.DoesStartWithDrivePattern(value).ShouldBeTrue();
        }

        [Fact]
        public void UncPatternIsMatchMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void DoesStartWithUncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartsWithUncPatternIsMatchMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternMultiFolderDirectory()
        {
            string winDirectory = string.Format("{0}server\\path\\test\\abc", _directoryStart);
            string unixDirectory = string.Format("{0}server/path/test/abc", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(13);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(13);
        }


        [Fact]
        public void UncPatternIsMatchExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void IsUncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void DoesStartWithUncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartsWithUncPatternIsMatchExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(13);
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternExactDirectory()
        {
            string winDirectory = string.Format("{0}server\\path", _directoryStart);
            string unixDirectory = string.Format("{0}server/path", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(13);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(13);
        }

        [Fact]
        public void UncPatternIsMatchMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void IsUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void DoesStartWithUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void StartsWithUncPatternIsMatchMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(9);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(9);
        }

        [Fact]
        public void UncPatternIsMatchTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);

        }

        [Fact]
        public void StartsWithUncPatternIsMatchTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBeTrue();

        }

        [Fact]
        public void DoesStartWithUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBeTrue();
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBeTrue();
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeTrue();
            match.Length.ShouldBe(9);
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}abc/def/", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(9);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(9);
        }

        [Fact]
        public void UncPatternIsMatchLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartsWithUncPatternIsMatchLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void DoesStartWithUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeFalse();

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeFalse();
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(-1);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(-1);
        }

        [Fact]
        public void UncPatternIsMatchNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartsWithUncPatternIsMatchNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void DoesStartWithUncPatternNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeFalse();

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeFalse();
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(-1);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(-1);
        }

        [Fact]
        public void UncPatternIsMatchEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void StartsWithUncPatternIsMatchEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void DoesStartWithUncPatternEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void RegexMatchLengthStartsWithUncEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            var match = FileUtilitiesRegex.StartsWithUncPattern.Match(winDirectory);
            match.Success.ShouldBeFalse();

            match = FileUtilitiesRegex.StartsWithUncPattern.Match(unixDirectory);
            match.Success.ShouldBeFalse();
        }

        [Fact]
        public void MatchLengthStartsWithUncPatternEmptyString()
        {
            string winDirectory = string.Format("", _directoryStart);
            string unixDirectory = string.Format("", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(winDirectory).ShouldBe(-1);
            FileUtilitiesRegex.DoesStartWithUncPatternMatchLength(unixDirectory).ShouldBe(-1);
        }
    }
}
