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
        private string _directoryStart = new string(Path.DirectorySeparatorChar, 2);
        private string _altDirectoryStart = new string(Path.AltDirectorySeparatorChar, 2);

        /// <summary>
        /// Tests for DrivePattern regex using valid drive patterns.
        /// </summary>
        [Fact]
        public void DrivePatternSuccessWithRegex()
        {
            string s;
            for(char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.DrivePattern.IsMatch(s).ShouldBe(true);
                s = (char)(i + ('a'-'A')) + ":";
                FileUtilitiesRegex.DrivePattern.IsMatch(s).ShouldBe(true);
            }
        }

        /// <summary>
        /// Tests IsDrivePattern using valid drive patterns.
        /// </summary>
        [Fact]
        public void DrivePatternSuccessWithoutRegex()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.IsDrivePattern(s).ShouldBe(true);
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.IsDrivePattern(s).ShouldBe(true);
            }
        }

        /// <summary>
        /// Tests DrivePattern regex using varopis invalid drive patterns.
        /// </summary>
        [Fact]
        public void DrivePatternFailureWithRegex()
        {
            FileUtilitiesRegex.DrivePattern.IsMatch("C: ").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch(" C:").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch(" :").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("CC:").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("::").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("C:/").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("C:\\").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("C\\").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch(":/").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("  ").ShouldBe(false);
            FileUtilitiesRegex.DrivePattern.IsMatch("").ShouldBe(false);
        }

        /// <summary>
        /// Tests DrivePattern regex using varopis invalid drive patterns.
        /// </summary>
        [Fact]
        public void DrivePatternFailureWithoutRegex()
        {
            FileUtilitiesRegex.IsDrivePattern("C: ").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern(" C:").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern(" :").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("CC:").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("::").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("C:/").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("C:\\").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("C\\").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern(":/").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("  ").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("").ShouldBe(false);
        }

        [Fact]
        public void UncPatternMinimumDirectory()
        {
            string winDirectory = string.Format("{0}a\\b", _directoryStart);
            string unixDirectory = string.Format("{0}a/b", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void UncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void UncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}a/b/", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void UncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void UncPatternNoSubfolder()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(false);
        }
    }
}
