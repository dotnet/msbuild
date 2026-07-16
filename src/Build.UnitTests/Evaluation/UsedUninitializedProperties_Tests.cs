// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;


namespace Microsoft.Build.Evaluation;

[TestClass]
public sealed class UsedUninitializedProperties_Tests
{
    [MSBuildTestMethod]
    public void Basics()
    {
        PropertiesUseTracker props = new(TestLoggingContext.CreateTestContext(new BuildEventContext(1, 2, 3, 4)));

        Assert.IsFalse(props.TryGetPropertyElementLocation("Hello", out IElementLocation? elementLocation));
        Assert.IsNull(elementLocation);

        props.RemoveProperty("Hello");

        IElementLocation location1 = new MockElementLocation("File1");
        IElementLocation location2 = new MockElementLocation("File2");

        props.TryAdd("Hello", location1);
        props.TryAdd("Hello", location2);

        Assert.IsTrue(props.TryGetPropertyElementLocation("Hello", out elementLocation));
        Assert.AreSame(location1, elementLocation);

        Assert.IsTrue(props.TryGetPropertyElementLocation("Hello", out elementLocation));
        Assert.AreSame(location1, elementLocation);

        props.RemoveProperty("Hello");

        Assert.IsFalse(props.TryGetPropertyElementLocation("Hello", out elementLocation));
        Assert.IsNull(elementLocation);

        props.RemoveProperty("Hello");
    }
}
