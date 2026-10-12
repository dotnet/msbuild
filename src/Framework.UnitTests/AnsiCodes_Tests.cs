// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class AnsiCodes_Tests
{
    [Theory]
    [InlineData((int)AnsiCodes.ProgramStatus.Idle, "idle")]
    [InlineData((int)AnsiCodes.ProgramStatus.Working, "working")]
    [InlineData((int)AnsiCodes.ProgramStatus.Done, "done")]
    [InlineData((int)AnsiCodes.ProgramStatus.Blocked, "blocked")]
    [InlineData((int)AnsiCodes.ProgramStatus.Error, "error")]
    [InlineData((int)AnsiCodes.ProgramStatus.Clear, "clear")]
    public void ProgramStatusFormatsAllStates(int status, string state)
    {
        AnsiCodes.SetProgramStatus((AnsiCodes.ProgramStatus)status).ShouldBe($"\x1b]7501;state={state}\x1b\\");
    }

    [Fact]
    public void ProgramStatusFormatsOptionalFields()
    {
        AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Working,
            id: "build/project-1/target-2", app: "msbuild", title: "Compile")
            .ShouldBe("\x1b]7501;state=working:id=build/project-1/target-2:app=msbuild:title=Q29tcGlsZQ==\x1b\\");
        AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Clear, id: "build")
            .ShouldBe("\x1b]7501;state=clear:id=build\x1b\\");
    }

    [Fact]
    public void ProgramStatusEncodesAndBoundsTitles()
    {
        string title = "\0\r\n\x1b\x7f\u0085" + new string('\u4e00', 57) + "\U00010000";
        string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(' ', 6) + new string('\u4e00', 57)));
        AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Working, title: title)
            .ShouldBe($"\x1b]7501;state=working:title={expected}\x1b\\");
        AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Working, title: new string('\u4e00', 65))
            .ShouldBe($"\x1b]7501;state=working:title={Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('\u4e00', 64)))}\x1b\\");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/build")]
    [InlineData("build/")]
    [InlineData("build//project")]
    [InlineData("build:state=error")]
    [InlineData("build\x1b\\")]
    [InlineData("a/b/c/d/e/f/g/h/i")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
    public void ProgramStatusRejectsInvalidRecordIds(string id)
    {
        Should.Throw<ArgumentException>(() => AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Working, id: id))
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void ProgramStatusRejectsInvalidStateAndApplication()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => AnsiCodes.SetProgramStatus((AnsiCodes.ProgramStatus)100))
            .ParamName.ShouldBe("status");
        Should.Throw<ArgumentException>(() => AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Working, app: "build/app"))
            .ParamName.ShouldBe("app");
        Should.Throw<ArgumentException>(() => AnsiCodes.SetProgramStatus(AnsiCodes.ProgramStatus.Working, id: new string('a', 129)))
            .ParamName.ShouldBe("id");
    }
}
