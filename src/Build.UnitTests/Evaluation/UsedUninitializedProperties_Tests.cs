// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;

using Xunit;

namespace Microsoft.Build.Evaluation;

public sealed class UsedUninitializedProperties_Tests
{
    [Fact]
    public void Basics()
    {
        UsedUninitializedProperties props = new();

        Assert.False(props.TryGetPropertyElementLocation("Hello", out IElementLocation? elementLocation));
        Assert.Null(elementLocation);

        props.RemoveProperty("Hello");

        IElementLocation location1 = new MockElementLocation("File1");
        IElementLocation location2 = new MockElementLocation("File2");

        props.TryAdd("Hello", location1);
        props.TryAdd("Hello", location2);

        Assert.True(props.TryGetPropertyElementLocation("Hello", out elementLocation));
        Assert.Same(location1, elementLocation);

        Assert.True(props.TryGetPropertyElementLocation("Hello", out elementLocation));
        Assert.Same(location1, elementLocation);

        props.RemoveProperty("Hello");

        Assert.False(props.TryGetPropertyElementLocation("Hello", out elementLocation));
        Assert.Null(elementLocation);

        props.RemoveProperty("Hello");
    }
}
