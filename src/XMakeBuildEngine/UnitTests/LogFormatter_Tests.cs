// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using System.Text.RegularExpressions;
using System.Globalization;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class LogFormatterTest
    {
        /*
        * Method:  TimeSpanMediumDuration
        * 
        * Tests the mainline: a medium length duration
        * Note the ToString overload used in FormatTimeSpan is culture insensitive.
        */
        [Fact]
        public void TimeSpanMediumDuration()
        {
            TimeSpan t = new TimeSpan(1254544900);
            string result = LogFormatter.FormatTimeSpan(t);
            Assert.Equal("00:02:05.45", result);
        }


        /*
        * Method:  TimeSpanZeroDuration
        * 
        * Format a TimeSpan where the duration is zero.
        * Note the ToString overload used in FormatTimeSpan is culture insensitive.
        */
        [Fact]
        public void TimeSpanZeroDuration()
        {
            TimeSpan t = new TimeSpan(0);
            string result = LogFormatter.FormatTimeSpan(t);
            Assert.Equal("00:00:00", result);
        }

        [Fact]
        public void FormatDateTime()
        {
            DateTime testTime = new DateTime(2007 /*Year*/, 08 /*Month*/, 20 /*Day*/, 10 /*Hour*/, 42 /*Minutes*/, 44 /*Seconds*/, 12 /*Milliseconds*/);
            string result = LogFormatter.FormatLogTimeStamp(testTime);

            Assert.Equal(testTime.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), result);

            testTime = new DateTime(2007, 08, 20, 05, 04, 03, 01);
            result = LogFormatter.FormatLogTimeStamp(testTime);
            Assert.Equal(testTime.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), result);

            testTime = new DateTime(2007, 08, 20, 0, 0, 0, 0);
            result = LogFormatter.FormatLogTimeStamp(testTime);
            Assert.Equal(testTime.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), result);
        }
    }
}





