// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Moq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class CreateNewImageTests
{
    [Theory]
    // Entrypoint, backwards compatibility.
    [InlineData("", "entrypointArg", "appCommand", "", "", "", null, new [] { "appCommand" }, new [] { "entrypointArg"})]
    // Entrypoint can be combined with all the other properties.
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "appCommandArg", "cmd", "", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArg", "cmd" })]
    // No Entrypoint, AppCommand specified, no base entrypoint.
    [InlineData("", "", "appCommand", "", "", "", null, new [] { "appCommand" }, null)]
    [InlineData("", "", "appCommand", "appCommandArg", "", "", null, new [] { "appCommand", "appCommandArg" }, null)]
    [InlineData("", "", "appCommand", "appCommandArg", "cmd", "", null, new [] { "appCommand", "appCommandArg" }, new[] { "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint.
    [InlineData("", "", "appCommand", "", "", "", "baseEntrypoint", new [] { "appCommand" }, null)]
    [InlineData("", "", "appCommand", "appCommandArg", "", "", "baseEntrypoint", new [] { "appCommand", "appCommandArg" }, null)]
    [InlineData("", "", "appCommand", "appCommandArg", "cmd", "", "baseEntrypoint", new [] { "appCommand", "appCommandArg" }, new[] { "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint, preserve base entrypoint by setting instruction to 'DefaultArgs'.
    [InlineData("", "", "appCommand", "", "", "DefaultArgs", "baseEntrypoint", new[] { "baseEntrypoint" }, new [] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArg", "", "DefaultArgs", "baseEntrypoint", new[] { "baseEntrypoint" }, new [] { "appCommand", "appCommandArg" })]
    [InlineData("", "", "appCommand", "appCommandArg", "cmd", "DefaultArgs", "baseEntrypoint", new[] { "baseEntrypoint" }, new [] { "appCommand", "appCommandArg", "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint = 'dotnet'.
    [InlineData("", "", "appCommand", "", "", "DefaultArgs", "dotnet", null, new [] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArg", "", "DefaultArgs", "dotnet", null, new [] { "appCommand", "appCommandArg" })]
    [InlineData("", "", "appCommand", "appCommandArg", "cmd", "DefaultArgs", "dotnet", null, new [] { "appCommand", "appCommandArg", "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint = '/usr/bin/dotnet'.
    [InlineData("", "", "appCommand", "", "", "DefaultArgs", "/usr/bin/dotnet", null, new [] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArg", "", "DefaultArgs", "/usr/bin/dotnet", null, new [] { "appCommand", "appCommandArg" })]
    [InlineData("", "", "appCommand", "appCommandArg", "cmd", "DefaultArgs", "/usr/bin/dotnet", null, new [] { "appCommand", "appCommandArg", "cmd" })]
    public void EntrypointAndCmd(string entrypoint, string entrypointArgs, string appCommand, string appCommandArg, string defaultArgs, string appCommandInstruction, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
    {
        CreateNewImage newImage = new()
        {
            Entrypoint = CreateTaskItems(entrypoint),
            EntrypointArgs = CreateTaskItems(entrypointArgs),
            DefaultArgs = CreateTaskItems(defaultArgs),
            AppCommand = CreateTaskItems(appCommand),
            AppCommandArgs = CreateTaskItems(appCommandArg),
            AppCommandInstruction = appCommandInstruction
        };

        newImage.BuildEngine = new Mock<IBuildEngine>().Object;

        (string[] imageEntrypoint, string[] imageCmd) = newImage.DetermineEntrypointAndCmd(baseImageEntrypoint?.Split(';'));

        Assert.False(newImage.Log.HasLoggedErrors);
        Assert.Equal(expectedEntrypoint ?? Array.Empty<string>(), imageEntrypoint);
        Assert.Equal(expectedCmd ?? Array.Empty<string>(), imageCmd);

        static ITaskItem[] CreateTaskItems(string value)
            => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => new TaskItem(s)).ToArray();
    }
}
