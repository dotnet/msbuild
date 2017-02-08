// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Text.RegularExpressions;


namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class LogFormatterTest
    {
        /*
        * Method:  TimeSpanMediumDuration
        * Owner:   jomof
        * 
        * Tests the mainline: a medium length duration
        */
        [Test]
        public void TimeSpanMediumDuration()
        {
            TimeSpan t = new TimeSpan(1254544900);
            string result = LogFormatter.FormatTimeSpan(t);
            Assertion.AssertEquals("00:02:05.45", result);            
        }


        /*
        * Method:  TimeSpanZeroDuration
        * Owner:   jomof
        * 
        * Format a TimeSpan where the duration is zero.
        */
        [Test]
        public void TimeSpanZeroDuration()
        {
            TimeSpan t = new TimeSpan(0);
            string result = LogFormatter.FormatTimeSpan(t);
            Assertion.AssertEquals("00:00:00", result);            
        }

        [Test]
        public void FormatDateTime()
        {

            DateTime testTime = new DateTime(2007 /*Year*/, 08 /*Month*/, 20 /*Day*/, 10 /*Hour*/, 42 /*Minutes*/, 44 /*Seconds*/, 12 /*Milliseconds*/);
            string result = LogFormatter.FormatLogTimeStamp(testTime);
            Assertion.AssertEquals("10:42:44.012", result);

            testTime = new DateTime(2007, 08, 20, 05, 04, 03, 01);
            result = LogFormatter.FormatLogTimeStamp(testTime);
            Assertion.AssertEquals("05:04:03.001", result);

            testTime = new DateTime(2007, 08, 20, 0, 0, 0, 0);
            result = LogFormatter.FormatLogTimeStamp(testTime);
            Assertion.AssertEquals("00:00:00.000", result);

        }

    }

}





