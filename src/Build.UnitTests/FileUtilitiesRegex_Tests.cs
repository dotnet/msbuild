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

        [Fact]
        public void DrivePatternIsMatchAllProperFormats()
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

        [Fact]
        public void IsDrivePatternAllProperFormats()
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

        [Fact]
        public void StartWithDrivePatternIsMatchAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.StartWithDrivePattern.IsMatch(s).ShouldBe(true);
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.StartWithDrivePattern.IsMatch(s).ShouldBe(true);
            }
        }



        [Fact]
        public void DoesStartWithDrivePatternAllProperFormats()
        {
            string s;
            for (char i = 'A'; i <= 'Z'; i++)
            {
                s = i + ":";
                FileUtilitiesRegex.DoesStartWithDrivePattern(s).ShouldBe(true);
                s = (char)(i + ('a' - 'A')) + ":";
                FileUtilitiesRegex.DoesStartWithDrivePattern(s).ShouldBe(true);
            }
        }

        [Fact]
        public void DrivePatternIsMatchInvalidFormat()
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
        public void IsDrivePatternInvalidFormat()
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

        /// <summary>
        /// Tests DrivePattern regex using varopis invalid drive patterns.
        /// </summary>
        [Fact]
        public void StartWithDriveIsMatchPatternInvalidFormat()
        {
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch(" C:").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch(" :").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("CC:").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("::").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("x\\").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch(":/").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("  ").ShouldBe(false);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("").ShouldBe(false);
        }

        /// <summary>
        /// Tests DrivePattern regex using varopis invalid drive patterns.
        /// </summary>
        [Fact]
        public void DoesStartWithDrivePatternInvalidFormat()
        {
            FileUtilitiesRegex.DoesStartWithDrivePattern(" C:").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern(" :").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern("CC:").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern("::").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern("x\\").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern(":/").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern("  ").ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithDrivePattern("").ShouldBe(false);
        }

        [Fact]
        public void StartWithDrivePatternIsMatchInvalidPatternValidStart()
        {
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("C: ").ShouldBe(true);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("C:/").ShouldBe(true);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("Z:\\").ShouldBe(true);
            FileUtilitiesRegex.StartWithDrivePattern.IsMatch("b:a\\q/").ShouldBe(true);
        }

        [Fact]
        public void DoesStartWithDrivePatternInvalidPatternValidStart()
        {
            FileUtilitiesRegex.DoesStartWithDrivePattern("C: ").ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithDrivePattern("C:/").ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithDrivePattern("Z:\\").ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithDrivePattern("b:a\\q/").ShouldBe(true);
        }


        [Fact]
        public void UncPatternIsMatchMinimumDirectory()
        {
            string winDirectory = string.Format("{0}a\\b", _directoryStart);
            string unixDirectory = string.Format("{0}a/b", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void IsUncPatternMinimumDirectory()
        {
            string winDirectory = string.Format("{0}a\\b", _directoryStart);
            string unixDirectory = string.Format("{0}a/b", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void DoesStartWithUncPatternMinimumDirectory()
        {
            string winDirectory = string.Format("{0}a\\b", _directoryStart);
            string unixDirectory = string.Format("{0}a/b", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void StartsWithUncPatternIsMatchMinimumDirectory()
        {
            string winDirectory = string.Format("{0}a\\b", _directoryStart);
            string unixDirectory = string.Format("{0}a/b", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void UncPatternIsMatchMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void IsUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void DoesStartWithUncPatternMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void StartsWithUncPatternIsMatchMixedSlashes()
        {
            string winDirectory = string.Format("{0}abc/def", _directoryStart);
            string unixDirectory = string.Format("{0}abc\\def", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);
        }

        [Fact]
        public void UncPatternIsMatchTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}a/b/", _altDirectoryStart);

            FileUtilitiesRegex.UncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.UncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void IsUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}a/b/", _altDirectoryStart);

            FileUtilitiesRegex.IsUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.IsUncPattern(unixDirectory).ShouldBe(false);

        }

        [Fact]
        public void StartsWithUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}a/b/", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(true);

        }

        [Fact]
        public void DoesStartWithUncPatternTrailingSlash()
        {
            string winDirectory = string.Format("{0}abc\\def\\", _directoryStart);
            string unixDirectory = string.Format("{0}a/b/", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(true);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(true);
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
        public void StartsWithUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}", _directoryStart);
            string unixDirectory = string.Format("{0}", _altDirectoryStart);

            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.StartsWithUncPattern.IsMatch(unixDirectory).ShouldBe(false);
        }

        [Fact]
        public void DoesStartWithUncPatternLessThanMinimum()
        {
            string winDirectory = string.Format("{0}server", _directoryStart);
            string unixDirectory = string.Format("{0}server", _altDirectoryStart);

            FileUtilitiesRegex.DoesStartWithUncPattern(winDirectory).ShouldBe(false);
            FileUtilitiesRegex.DoesStartWithUncPattern(unixDirectory).ShouldBe(false);
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
        public void StartsWithUncPatternNoSubfolder()
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
        public void StartsWithUncEmptyString()
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
    }
}
