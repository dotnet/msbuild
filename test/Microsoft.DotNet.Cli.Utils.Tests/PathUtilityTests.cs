// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PathUtilityTests : TestBase
    {
        /// <summary>
        /// Tests that PathUtility.GetRelativePath treats drive references as case insensitive on Windows.
        /// </summary>
        [WindowsOnlyFact]
        public void GetRelativePathWithCaseInsensitiveDrives()
        {
            Assert.Equal(@"bar\", PathUtility.GetRelativePath(@"C:\foo\", @"C:\foo\bar\"));
            Assert.Equal(@"Bar\Baz\", PathUtility.GetRelativePath(@"c:\foo\", @"C:\Foo\Bar\Baz\"));
            Assert.Equal(@"baz\Qux\", PathUtility.GetRelativePath(@"C:\fOO\bar\", @"c:\foo\BAR\baz\Qux\"));
            Assert.Equal(@"d:\foo\", PathUtility.GetRelativePath(@"C:\foo\", @"d:\foo\"));
        }

        /// <summary>
        /// Tests that PathUtility.RemoveExtraPathSeparators works correctly with drive references on Windows.
        /// </summary>
        [WindowsOnlyFact]
        public void RemoveExtraPathSeparatorsWithDrives()
        {
            Assert.Equal(@"c:\foo\bar\baz\", PathUtility.RemoveExtraPathSeparators(@"c:\\\foo\\\\bar\baz\\"));
            Assert.Equal(@"D:\QUX\", PathUtility.RemoveExtraPathSeparators(@"D:\\\\\QUX\"));
        }
    }
}
