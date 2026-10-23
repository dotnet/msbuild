// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests;

public sealed class DeclaredIOAttribute_Tests
{
    [Fact]
    public void AttributesExposeDeclaredConfiguration()
    {
        MSBuildDeclaredIOTaskAttribute? taskAttribute =
            typeof(DeclaredIOTask)
                .GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>();
        MSBuildDeclaredIORequiresUnsetAttribute? requiresUnsetAttribute =
            typeof(DeclaredIOTask)
                .GetCustomAttribute<MSBuildDeclaredIORequiresUnsetAttribute>();

        taskAttribute.ShouldNotBeNull();
        requiresUnsetAttribute.ShouldNotBeNull();
        requiresUnsetAttribute.ParameterName.ShouldBe("UncacheableMode");
    }

    [MSBuildDeclaredIOTask]
    [MSBuildDeclaredIORequiresUnset("UncacheableMode")]
    private sealed class DeclaredIOTask;
}
