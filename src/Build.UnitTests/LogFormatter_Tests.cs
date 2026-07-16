// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.BackEnd.Logging;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class LogFormatterTest
    {
        /*
        * Method:  TimeSpanMediumDuration
        *
        * Tests the mainline: a medium length duration
        * Note the ToString overload used in FormatTimeSpan is culture insensitive.
        */
        [MSBuildTestMethod]
        public void TimeSpanMediumDuration()
        {
            TimeSpan t = new TimeSpan(1254544900);
            string result = LogFormatter.FormatTimeSpan(t);
            Assert.AreEqual("00:02:05.45", result);
        }


        /*
        * Method:  TimeSpanZeroDuration
        *
        * Format a TimeSpan where the duration is zero.
        * Note the ToString overload used in FormatTimeSpan is culture insensitive.
        */
        [MSBuildTestMethod]
        public void TimeSpanZeroDuration()
        {
            TimeSpan t = new TimeSpan(0);
            string result = LogFormatter.FormatTimeSpan(t);
            Assert.AreEqual("00:00:00", result);
        }

        [MSBuildTestMethod]
        public void FormatDateTime()
        {
            DateTime testTime = new DateTime(2007 /*Year*/, 08 /*Month*/, 20 /*Day*/, 10 /*Hour*/, 42 /*Minutes*/, 44 /*Seconds*/, 12 /*Milliseconds*/);
            string result = LogFormatter.FormatLogTimeStamp(testTime);

            Assert.AreEqual(testTime.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), result);

            testTime = new DateTime(2007, 08, 20, 05, 04, 03, 01);
            result = LogFormatter.FormatLogTimeStamp(testTime);
            Assert.AreEqual(testTime.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), result);

            testTime = new DateTime(2007, 08, 20, 0, 0, 0, 0);
            result = LogFormatter.FormatLogTimeStamp(testTime);
            Assert.AreEqual(testTime.ToString("HH:mm:ss.fff", CultureInfo.CurrentCulture), result);
        }
    }
}
