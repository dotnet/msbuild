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
        }
    }
}
