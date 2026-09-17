// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the <see cref="FILETIME"/> extension helpers.
    /// </summary>
    public class FileTimeExtensions_Tests
    {
        [Fact]
        public void ToLong_RoundTripsThroughDateTime()
        {
            DateTime utc = new(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
            long ticks = utc.ToFileTimeUtc();

            FILETIME ft = new()
            {
                dwLowDateTime = (int)(ticks & 0xFFFFFFFF),
                dwHighDateTime = (int)(ticks >> 32),
            };

            ft.ToLong().ShouldBe(ticks);
        }

        [Fact]
        public void ToDateTime_ReturnsLocalDateTime()
        {
            DateTime utc = new(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
            long ticks = utc.ToFileTimeUtc();

            FILETIME ft = new()
            {
                dwLowDateTime = (int)(ticks & 0xFFFFFFFF),
                dwHighDateTime = (int)(ticks >> 32),
            };

            DateTime result = ft.ToDateTime();
            result.Kind.ShouldBe(DateTimeKind.Local);
            result.ShouldBe(utc.ToLocalTime());
        }

        [Fact]
        public void ToDateTimeUtc_ReturnsUtcDateTime()
        {
            DateTime utc = new(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
            long ticks = utc.ToFileTimeUtc();

            FILETIME ft = new()
            {
                dwLowDateTime = (int)(ticks & 0xFFFFFFFF),
                dwHighDateTime = (int)(ticks >> 32),
            };

            DateTime result = ft.ToDateTimeUtc();
            result.Kind.ShouldBe(DateTimeKind.Utc);
            result.ShouldBe(utc);
        }

        [Fact]
        public void ToLong_HighBitInLowField_ReadsAsUnsigned()
        {
            // dwLowDateTime is uint at the FILETIME level on Win32; ComTypes uses int.
            // We must treat it as unsigned to avoid sign-extension into the upper 32 bits.
            FILETIME ft = new()
            {
                dwLowDateTime = unchecked((int)0xFFFFFFFF),
                dwHighDateTime = 0,
            };

            ft.ToLong().ShouldBe(0xFFFFFFFFL);
        }
    }
}
