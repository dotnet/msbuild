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
    [InlineData("", "entrypointArg", "", "appCommand", "", "", null, new [] { "appCommand" }, new [] { "entrypointArg"})]
    // Entrypoint can be combined with all the other properties.
    [InlineData("entrypoint", "entrypointArgs", "cmd", "appCommand", "appCommandArgs", "", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "cmd" })]
    // No Entrypoint, AppCommand specified, no base entrypoint.
    [InlineData("", "", "", "appCommand", "", "", null, new [] { "appCommand" }, null)]
    [InlineData("", "", "", "appCommand", "appCommandArg", "", null, new [] { "appCommand", "appCommandArg" }, null)]
    [InlineData("", "", "cmd", "appCommand", "appCommandArg", "", null, new [] { "appCommand", "appCommandArg" }, new[] { "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint.
    [InlineData("", "", "", "appCommand", "", "", "baseEntrypoint", new [] { "appCommand" }, null)]
    [InlineData("", "", "", "appCommand", "appCommandArg", "", "baseEntrypoint", new [] { "appCommand", "appCommandArg" }, null)]
    [InlineData("", "", "cmd", "appCommand", "appCommandArg", "", "baseEntrypoint", new [] { "appCommand", "appCommandArg" }, new[] { "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint, preserve base entrypoint by setting instruction to 'Cmd'.
    [InlineData("", "", "", "appCommand", "", "Cmd", "baseEntrypoint", new[] { "baseEntrypoint" }, new [] { "appCommand" })]
    [InlineData("", "", "", "appCommand", "appCommandArg", "Cmd", "baseEntrypoint", new[] { "baseEntrypoint" }, new [] { "appCommand", "appCommandArg" })]
    [InlineData("", "", "cmd", "appCommand", "appCommandArg", "Cmd", "baseEntrypoint", new[] { "baseEntrypoint" }, new [] { "appCommand", "appCommandArg", "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint = 'dotnet'.
    [InlineData("", "", "", "appCommand", "", "Cmd", "dotnet", null, new [] { "appCommand" })]
    [InlineData("", "", "", "appCommand", "appCommandArg", "Cmd", "dotnet", null, new [] { "appCommand", "appCommandArg" })]
    [InlineData("", "", "cmd", "appCommand", "appCommandArg", "Cmd", "dotnet", null, new [] { "appCommand", "appCommandArg", "cmd" })]
    // No Entrypoint, AppCommand specified, base entrypoint = '/usr/bin/dotnet'.
    [InlineData("", "", "", "appCommand", "", "Cmd", "/usr/bin/dotnet", null, new [] { "appCommand" })]
    [InlineData("", "", "", "appCommand", "appCommandArg", "Cmd", "/usr/bin/dotnet", null, new [] { "appCommand", "appCommandArg" })]
    [InlineData("", "", "cmd", "appCommand", "appCommandArg", "Cmd", "/usr/bin/dotnet", null, new [] { "appCommand", "appCommandArg", "cmd" })]
    public void EntrypointAndCmd(string entrypoint, string entrypointArgs, string cmd, string appCommand, string appCommandArgs, string appCommandInstruction, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
    {
        CreateNewImage newImage = new()
        {
            Entrypoint = CreateTaskItems(entrypoint),
            EntrypointArgs = CreateTaskItems(entrypointArgs),
            Cmd = CreateTaskItems(cmd),
            AppCommand = CreateTaskItems(appCommand),
            AppCommandArgs = CreateTaskItems(appCommandArgs),
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
