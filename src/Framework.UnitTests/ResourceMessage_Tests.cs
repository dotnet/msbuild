// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;
using static Microsoft.Build.Framework.Utilities.MSBuildResources;

namespace Microsoft.Build.UnitTests;

public class ResourceMessage_Tests
{
    [Fact]
    public void FactoryWithoutArgsPreservesResourceName()
    {
        Verify(MSBuildResource("ResourceName"), []);
    }

    [Fact]
    public void FactoriesPreserveResourceNameAndArgs()
    {
        Verify(MSBuildResource("ResourceName").WithArgs(1), [1]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2), [1, 2]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2, 3), [1, 2, 3]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2, 3, 4), [1, 2, 3, 4]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2, 3, 4, 5), [1, 2, 3, 4, 5]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2, 3, 4, 5, 6), [1, 2, 3, 4, 5, 6]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2, 3, 4, 5, 6, 7), [1, 2, 3, 4, 5, 6, 7]);
        Verify(MSBuildResource("ResourceName").WithArgs(1, 2, 3, 4, 5, 6, 7, 8), [1, 2, 3, 4, 5, 6, 7, 8]);
    }

    private static void Verify<TArgs>(ResourceMessage<TArgs> message, object?[] expectedArgs)
        where TArgs : struct, IMessageArgs
    {
        message.ResourceName.ShouldBe("ResourceName");
        message.Args.Count.ShouldBe(expectedArgs.Length);

        for (int i = 0; i < expectedArgs.Length; i++)
        {
            message.Args[i].ShouldBe(expectedArgs[i]);
        }

        string format = string.Join(" ", Enumerable.Range(0, message.Args.Count).Select(i => $"{{{i}}}"));
        message.Format(format).ShouldBe(string.Join(" ", expectedArgs));
    }
}
