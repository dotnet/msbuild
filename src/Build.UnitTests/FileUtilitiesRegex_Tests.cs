using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Shouldly;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Engine.UnitTests
{
    public class FileUtilitiesRegex_Tests
    {
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
        /// Tests for IsDrivePattern using valid drive patterns.
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
        /// Tests for DrivePattern regex using invalid drive patterns.
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
            FileUtilitiesRegex.IsDrivePattern("  ").ShouldBe(false);
            FileUtilitiesRegex.IsDrivePattern("").ShouldBe(false);
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
    }
}
