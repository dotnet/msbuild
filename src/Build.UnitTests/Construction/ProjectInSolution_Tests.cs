// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.Build.Construction;

public sealed class ProjectInSolution_Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Hello", "Hello")]
    [InlineData("Hello.world", "Hello_world")]
    [InlineData("Hello_world", "Hello_world")]
    [InlineData("Hello (world)", "Hello _world_")]
    [InlineData("It's 99.9% bug free", "It_s 99_9_ bug free")]
    [InlineData("%$@;.()'", "________")]
    public void CleanseProjectName(string input, string expected)
    {
        // Disallowed characters are: %$@;.()'
        string actual = ProjectInSolution.CleanseProjectName(input);

        Assert.Equal(expected, actual);

        if (input == expected)
        {
            Assert.Same(input, actual);
        }
    }
}
