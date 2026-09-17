// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class MessageParserTests
{
    [Theory]
    [InlineData("MSB7007: This is a message.")] // most common message pattern
    [InlineData("MSB7007:This is a message.")] // no whitespace between colon and message is ok
    [InlineData("  MSB7007:   This is a message.")] // whitespace before code and after colon is ok
    public void TryParseMSBuildCode_ValidCode(string input)
    {
        MessageParser.TryParseMSBuildCode(input, out string? code, out string? message).ShouldBeTrue();
        code.ShouldBe("MSB7007");
        message.ShouldBe("This is a message.");
    }

    [Theory]
    [InlineData("MSB7007:")] // code with no message yields an empty message
    [InlineData("MSB7007:   ")] // code followed by only whitespace yields an empty message
    public void TryParseMSBuildCode_CodeOnly(string input)
    {
        MessageParser.TryParseMSBuildCode(input, out string? code, out string? message).ShouldBeTrue();
        code.ShouldBe("MSB7007");
        message.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("MSB7007 : This is a message.")] // whitespace between code and colon is not ok
    [InlineData("MSB 7007: This is a message.")] // whitespace in code is not ok
    [InlineData("MSB007: This is a message.")] // code with less than 4 digits is not ok
    [InlineData("MSB: This is a message.")] // code without digits is not ok
    [InlineData("1001: This is a message.")] // code without MSB prefix is not ok
    [InlineData("7001MSB: This is a message.")] // digits before MSB prefix is not ok
    [InlineData("MSB564B: This is a message.")] // mixing letters and digits is not ok
    [InlineData("msb1001: This is a message.")] // lowercase code is not ok
    public void TryParseMSBuildCode_InvalidCode(string input)
    {
        MessageParser.TryParseMSBuildCode(input, out string? code, out string? message).ShouldBeFalse();
        code.ShouldBeNull();
        message.ShouldBeNull();
    }

    [Theory]
    [InlineData("MYTASK1001: This is a message.", "MYTASK1001")] // arbitrary letter prefix is ok
    [InlineData("MSB7007: This is a message.", "MSB7007")] // MSBuild codes are also matched
    [InlineData("  CS1002:   This is a message.", "CS1002")] // whitespace before code and after colon is ok
    public void TryParseAnyCode_ValidCode(string input, string expectedCode)
    {
        MessageParser.TryParseAnyCode(input, out string? code, out string? message).ShouldBeTrue();
        code.ShouldBe(expectedCode);
        message.ShouldBe("This is a message.");
    }

    [Theory]
    [InlineData("MYTASK1001:", "MYTASK1001")] // code with no message yields an empty message
    [InlineData("CS1002:   ", "CS1002")] // code followed by only whitespace yields an empty message
    public void TryParseAnyCode_CodeOnly(string input, string expectedCode)
    {
        MessageParser.TryParseAnyCode(input, out string? code, out string? message).ShouldBeTrue();
        code.ShouldBe(expectedCode);
        message.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("MSB7007 : This is a message.")] // whitespace between code and colon is not ok
    [InlineData("1001: This is a message.")] // code without a letter prefix is not ok
    [InlineData("MSB: This is a message.")] // code without digits is not ok
    [InlineData("MSB564B: This is a message.")] // trailing letters after digits is not ok
    [InlineData("This is a message.")] // no code at all
    public void TryParseAnyCode_InvalidCode(string input)
    {
        MessageParser.TryParseAnyCode(input, out string? code, out string? message).ShouldBeFalse();
        code.ShouldBeNull();
        message.ShouldBeNull();
    }

    [Theory]
    [InlineData("MSB7007: This is a message.")] // most common message pattern
    [InlineData("MSB7007:This is a message.")] // no whitespace between colon and message is ok
    [InlineData("  MSB7007:   This is a message.")] // whitespace before code and after colon is ok
    [InlineData("MSB7007:")] // code with no message is ok
    public void TryGetMSBuildCode_ValidCode(string input)
    {
        MessageParser.TryGetMSBuildCode(input, out string? code).ShouldBeTrue();
        code.ShouldBe("MSB7007");
    }

    [Theory]
    [InlineData("MSB7007 : This is a message.")] // whitespace between code and colon is not ok
    [InlineData("MSB 7007: This is a message.")] // whitespace in code is not ok
    [InlineData("MSB007: This is a message.")] // code with less than 4 digits is not ok
    [InlineData("MSB: This is a message.")] // code without digits is not ok
    [InlineData("1001: This is a message.")] // code without MSB prefix is not ok
    [InlineData("MSB564B: This is a message.")] // mixing letters and digits is not ok
    [InlineData("msb1001: This is a message.")] // lowercase code is not ok
    public void TryGetMSBuildCode_InvalidCode(string input)
    {
        MessageParser.TryGetMSBuildCode(input, out string? code).ShouldBeFalse();
        code.ShouldBeNull();
    }

    [Theory]
    [InlineData("MYTASK1001: This is a message.", "MYTASK1001")] // arbitrary letter prefix is ok
    [InlineData("MSB7007: This is a message.", "MSB7007")] // MSBuild codes are also matched
    [InlineData("  CS1002:   This is a message.", "CS1002")] // whitespace before code and after colon is ok
    [InlineData("CS1002:", "CS1002")] // code with no message is ok
    public void TryGetAnyCode_ValidCode(string input, string expectedCode)
    {
        MessageParser.TryGetAnyCode(input, out string? code).ShouldBeTrue();
        code.ShouldBe(expectedCode);
    }

    [Theory]
    [InlineData("MSB7007 : This is a message.")] // whitespace between code and colon is not ok
    [InlineData("1001: This is a message.")] // code without a letter prefix is not ok
    [InlineData("MSB: This is a message.")] // code without digits is not ok
    [InlineData("MSB564B: This is a message.")] // trailing letters after digits is not ok
    [InlineData("This is a message.")] // no code at all
    public void TryGetAnyCode_InvalidCode(string input)
    {
        MessageParser.TryGetAnyCode(input, out string? code).ShouldBeFalse();
        code.ShouldBeNull();
    }

    [Theory]
    [InlineData("MSB7007: This is a message.")] // most common message pattern
    [InlineData("MSB7007:This is a message.")] // no whitespace between colon and message is ok
    [InlineData("  MSB7007:   This is a message.")] // whitespace before code and after colon is ok
    public void TryStripMSBuildCode_ValidCode(string input)
    {
        MessageParser.TryStripMSBuildCode(input, out string? message).ShouldBeTrue();
        message.ShouldBe("This is a message.");
    }

    [Theory]
    [InlineData("MSB7007:")] // code with no message yields an empty message
    [InlineData("MSB7007:   ")] // code followed by only whitespace yields an empty message
    public void TryStripMSBuildCode_CodeOnly(string input)
    {
        MessageParser.TryStripMSBuildCode(input, out string? message).ShouldBeTrue();
        message.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("MSB7007 : This is a message.")] // whitespace between code and colon is not ok
    [InlineData("MSB 7007: This is a message.")] // whitespace in code is not ok
    [InlineData("MSB007: This is a message.")] // code with less than 4 digits is not ok
    [InlineData("MSB: This is a message.")] // code without digits is not ok
    [InlineData("1001: This is a message.")] // code without MSB prefix is not ok
    [InlineData("MSB564B: This is a message.")] // mixing letters and digits is not ok
    [InlineData("msb1001: This is a message.")] // lowercase code is not ok
    public void TryStripMSBuildCode_InvalidCode(string input)
    {
        MessageParser.TryStripMSBuildCode(input, out string? message).ShouldBeFalse();
        message.ShouldBeNull();
    }

    [Theory]
    [InlineData("MYTASK1001: This is a message.")] // arbitrary letter prefix is ok
    [InlineData("MSB7007: This is a message.")] // MSBuild codes are also matched
    [InlineData("  CS1002:   This is a message.")] // whitespace before code and after colon is ok
    public void TryStripAnyCode_ValidCode(string input)
    {
        MessageParser.TryStripAnyCode(input, out string? message).ShouldBeTrue();
        message.ShouldBe("This is a message.");
    }

    [Theory]
    [InlineData("MYTASK1001:")] // code with no message yields an empty message
    [InlineData("CS1002:   ")] // code followed by only whitespace yields an empty message
    public void TryStripAnyCode_CodeOnly(string input)
    {
        MessageParser.TryStripAnyCode(input, out string? message).ShouldBeTrue();
        message.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("MSB7007 : This is a message.")] // whitespace between code and colon is not ok
    [InlineData("1001: This is a message.")] // code without a letter prefix is not ok
    [InlineData("MSB: This is a message.")] // code without digits is not ok
    [InlineData("MSB564B: This is a message.")] // trailing letters after digits is not ok
    [InlineData("This is a message.")] // no code at all
    public void TryStripAnyCode_InvalidCode(string input)
    {
        MessageParser.TryStripAnyCode(input, out string? message).ShouldBeFalse();
        message.ShouldBeNull();
    }
}
