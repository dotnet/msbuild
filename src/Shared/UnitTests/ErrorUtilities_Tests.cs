// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class ErrorUtilities_Tests
{
    [Fact]
    public void VerifyThrowFalse()
    {
        var ex = Assert.Throws<InternalErrorException>(() =>
        {
            ErrorUtilities.VerifyThrow(false, "msbuild rules");
        });

        Assert.Contains("msbuild rules", ex.Message);
    }

    [Fact]
    public void VerifyThrowTrue()
    {
        // This shouldn't throw.
        ErrorUtilities.VerifyThrow(true, "msbuild rules");
    }

    [Fact]
    public void VerifyThrow_InterpolatedString_DoesNotFormat_WhenConditionIsTrue()
    {
        bool formatted = false;
        ErrorUtilities.VerifyThrow(true, $"message {FormatSideEffect(ref formatted)}");
        Assert.False(formatted, "Interpolated string should not have been formatted when condition is true");
    }

    [Fact]
    public void VerifyThrow_InterpolatedString_Formats_WhenConditionIsFalse()
    {
        bool formatted = false;
        var ex = Assert.Throws<InternalErrorException>(() =>
        {
            ErrorUtilities.VerifyThrow(false, $"error: {FormatSideEffect(ref formatted)}");
        });

        Assert.True(formatted, "Interpolated string should have been formatted when condition is false");
        Assert.Contains("error: formatted", ex.Message);
    }

    [Fact]
    public void VerifyThrow_InterpolatedString_FormatsMultipleArgs_WhenConditionIsFalse()
    {
        var ex = Assert.Throws<InternalErrorException>(() =>
        {
            ErrorUtilities.VerifyThrow(false, $"a={1} b={2} c={"three"}");
        });

        Assert.Contains("a=1 b=2 c=three", ex.Message);
    }

    private static string FormatSideEffect(ref bool formatted)
    {
        formatted = true;
        return "formatted";
    }
}
