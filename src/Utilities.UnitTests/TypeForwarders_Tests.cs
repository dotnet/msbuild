// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Microsoft.Build.UnitTests;

public class TypeForwarders_Tests
{
    [Fact]
    public void ToolLocationHelperLeafTypesAreForwardedFromUtilities()
    {
        Type[] expectedTypes =
        [
            typeof(DotNetFrameworkArchitecture),
            typeof(MultipleVersionSupport),
            typeof(ProcessorArchitecture),
            typeof(SDKType),
            typeof(TargetDotNetFrameworkVersion),
            typeof(VisualStudioVersion),
        ];

        Assembly utilitiesAssembly = typeof(Task).Assembly;
        Assembly frameworkAssembly = typeof(IBuildEngine).Assembly;

        foreach (Type expectedType in expectedTypes)
        {
            Type resolvedType = Type
                .GetType(
                    $"{expectedType.FullName}, {utilitiesAssembly.FullName}",
                    throwOnError: true,
                    ignoreCase: false)
                .ShouldNotBeNull();

            resolvedType.ShouldBe(expectedType);
            resolvedType.Assembly.ShouldBe(frameworkAssembly);
        }
    }
}
