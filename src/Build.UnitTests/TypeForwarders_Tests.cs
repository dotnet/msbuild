// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class TypeForwarders_Tests
{
    [Fact]
    public void FileAccessTypesAreForwardedFromBuild()
    {
        Type[] expectedTypes =
        [
            typeof(DesiredAccess),
            typeof(FileAccessData),
            typeof(FlagsAndAttributes),
            typeof(ProcessData),
            typeof(ReportedFileOperation),
            typeof(RequestedAccess),
        ];

        Assembly buildAssembly = typeof(ProjectInstance).Assembly;
        Assembly frameworkAssembly = typeof(IBuildEngine).Assembly;

        foreach (Type expectedType in expectedTypes)
        {
            Type resolvedType = Type.GetType($"{expectedType.FullName}, {buildAssembly.FullName}", throwOnError: true, ignoreCase: false)
                .ShouldNotBeNull();

            resolvedType.ShouldBe(expectedType);
            resolvedType.Assembly.ShouldBe(frameworkAssembly);
        }
    }
}
