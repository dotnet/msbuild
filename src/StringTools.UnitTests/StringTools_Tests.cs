// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET35_UNITTEST
extern alias StringToolsNet35;
#endif

using System;

using Shouldly;
using Xunit;

#if NET35_UNITTEST
using StringToolsNet35::Microsoft.NET.StringTools;
using Shouldly.Configuration;
#else
using Microsoft.NET.StringTools;
#endif

#nullable disable

namespace Microsoft.NET.StringTools.Tests
{
    public class StringTools_Tests
    {
        [Theory]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("Hello")]
        [InlineData("HelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHelloHello")]
        public void InternsStrings(string str)
        {
            string internedString1 = Strings.WeakIntern(str);
            internedString1.Equals(str).ShouldBeTrue();
            string internedString2 = Strings.WeakIntern(str);
            internedString1.Equals(str).ShouldBeTrue();
            Object.ReferenceEquals(internedString1, internedString2).ShouldBeTrue();

#if !NET35_UNITTEST
            ReadOnlySpan<char> span = str.AsSpan();
            internedString1 = Strings.WeakIntern(span);
            internedString1.Equals(str).ShouldBeTrue();
            internedString2 = Strings.WeakIntern(span);
            internedString1.Equals(str).ShouldBeTrue();
            Object.ReferenceEquals(internedString1, internedString2).ShouldBeTrue();
#endif
        }

        [Fact]
        public void CreatesDiagnosticReport()
        {
            string statisticsNotEnabledString = "EnableStatisticsGathering() has not been called";

            Strings.CreateDiagnosticReport().ShouldContain(statisticsNotEnabledString);

            Strings.EnableDiagnostics();
            string report = Strings.CreateDiagnosticReport();

            report.ShouldNotContain(statisticsNotEnabledString);
            report.ShouldContain("Eliminated Strings");
        }
    }
}
