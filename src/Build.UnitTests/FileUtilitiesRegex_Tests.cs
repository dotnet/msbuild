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
        private string directoryStart = new string(Path.DirectorySeparatorChar, 2);
        private string altDirectoryStart = new string(Path.AltDirectorySeparatorChar, 2);

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
        /// Tests DrivePattern regex using invalid drive patterns.
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
        /// Tests for DrivePattern regex using invalid drive patterns.
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
            string winDirectory = string.Format("{0}{0}a\\b", Path.DirectorySeparatorChar);
            string unixDirectory = string.Format("{0}{0}a\\b", Path.AltDirectorySeparatorChar);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void UncPatternMixedSlashes()
        {
            FileUtilitiesRegex.UncPattern.IsMatch(directoryStart + "abc/def").ShouldBe(true);
            FileUtilitiesRegex.UncPattern.IsMatch(altDirectoryStart + "abc\\def").ShouldBe(true);

            FileUtilitiesRegex.DoesStartWithUncPattern(directoryStart + "abc/def").ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(altDirectoryStart + "abc\\def").ShouldBe(true);
        }

        [Fact]
        public void UncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}{0}abc\\def\\", Path.DirectorySeparatorChar);
            string unixDirectory = string.Format("{0}{0}a/b/", Path.AltDirectorySeparatorChar);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);


        }
    }
}
