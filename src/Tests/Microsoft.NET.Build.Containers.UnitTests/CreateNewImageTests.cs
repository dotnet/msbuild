// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class CreateNewImageTests
{
    [Theory]
    // Entrypoint, backwards compatibility.
    [InlineData("", "entrypointArg", "appCommand", "", "", null, new[] { "appCommand" }, new[] { "entrypointArg" })]
    // When no entrypoint is specified, emit the AppCommand as the Entrypoint.
    [InlineData("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "appCommand", "appCommandArgs" }, new[] { "defaultArgs" })]
    // Set all properties. When an entrypoint is specified, emit the AppCommand as Cmd.
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs",
                "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    public void EntrypointAndCmd_NoInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [Theory]
    // Set all properties.
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs",
                                                                       "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, base entrypoint is preserved.
    [InlineData("", "", "appCommand", "", "", "", null, new[] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "", "", null, new[] { "appCommand", "appCommandArgs" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "defaultArgs", "", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    [InlineData("", "", "appCommand", "", "", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand", "appCommandArgs" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, 'dotnet' base entrypoint is ignored.
    [InlineData("", "", "appCommand", "", "", "dotnet", null, new[] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "", "dotnet", null, new[] { "appCommand", "appCommandArgs" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "defaultArgs", "dotnet", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, '/usr/bin/dotnet' base entrypoint is ignored.
    [InlineData("", "", "appCommand", "", "", "/usr/bin/dotnet", null, new[] { "appCommand" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "", "/usr/bin/dotnet", null, new[] { "appCommand", "appCommandArgs" })]
    [InlineData("", "", "appCommand", "appCommandArgs", "defaultArgs", "/usr/bin/dotnet", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    public void EntrypointAndCmd_DefaultArgsInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("DefaultArgs", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [Theory]
    // Set all properties except entrypoint and entrypointArgs.
    [InlineData("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "appCommand", "appCommandArgs" }, new[] { "defaultArgs" })]
    // Can't set entrypoint or entrypointArgs with instruction 'Entrypoint'.
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [InlineData("", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [InlineData("entrypoint", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_EntrypointInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("Entrypoint", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [Theory]
    // Set all properties except appCommand and appCommandArgs.
    [InlineData("entrypoint", "entrypointArgs", "", "", "defaultArgs", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "defaultArgs" })]
    // Can't set appCommand or appCommandArgs with instruction 'None'.
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [InlineData("entrypoint", "entrypointArgs", "", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_NoneInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("None", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [Theory]
    // Set all properties accepted.
    [InlineData("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // Set all properties except entrypoint fails: can't set entrypointArgs without setting entrypoint.
    [InlineData("", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    // Set all properties except appCommand fails: can't set appCommandArgs without setting appCommand.
    [InlineData("entrypoint", "entrypointArgs", "", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_RequiredProperties(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("DefaultArgs", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    private static void ValidateArgsAndCmd(string appCommandInstruction, string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
    {
        var newImage = new CreateNewImage()
        {
            Entrypoint = CreateTaskItems(entrypoint),
            EntrypointArgs = CreateTaskItems(entrypointArgs),
            DefaultArgs = CreateTaskItems(defaultArgs),
            AppCommand = CreateTaskItems(appCommand),
            AppCommandArgs = CreateTaskItems(appCommandArgs),
            AppCommandInstruction = appCommandInstruction,
            BuildEngine = new Mock<IBuildEngine>().Object
        };

        (string[] imageEntrypoint, string[] imageCmd) = newImage.DetermineEntrypointAndCmd(baseImageEntrypoint?.Split(';', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(newImage.Log.HasLoggedErrors, imageEntrypoint.Length == 0 && imageCmd.Length == 0);
        Assert.Equal(expectedEntrypoint ?? Array.Empty<string>(), imageEntrypoint);
        Assert.Equal(expectedCmd ?? Array.Empty<string>(), imageCmd);

        static ITaskItem[] CreateTaskItems(string value)
            => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => new TaskItem(s)).ToArray();
    }
}
