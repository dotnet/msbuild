// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class DeclaredIOTask_Tests
{
    [Fact]
    public void ReadLinesDeclaresTheFileButNotItsReturnedTextAsFileIO()
    {
        typeof(ReadLinesFromFile).GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>().ShouldNotBeNull();
        typeof(ReadLinesFromFile).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>()
            .Select(attribute => attribute.ParameterName).ShouldBe([nameof(ReadLinesFromFile.File)]);
        typeof(ReadLinesFromFile).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>().ShouldBeEmpty();
        typeof(ReadLinesFromFile).GetProperty(nameof(ReadLinesFromFile.Lines))!.GetCustomAttribute<OutputAttribute>().ShouldNotBeNull();
    }

    [Fact]
    public void WriteLinesDoesNotDeclareIOBecauseItsModesCanReadAndWriteTheSameFile()
    {
        typeof(WriteLinesToFile).GetCustomAttribute<MSBuildDeclaredIOTaskAttribute>().ShouldBeNull();
        typeof(WriteLinesToFile).GetCustomAttributes<MSBuildDeclaredIOInputAttribute>().ShouldBeEmpty();
        typeof(WriteLinesToFile).GetCustomAttributes<MSBuildDeclaredIOOutputAttribute>().ShouldBeEmpty();
    }
}
