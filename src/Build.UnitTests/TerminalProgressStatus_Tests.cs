// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class TerminalProgressStatus_Tests
{
    [Fact]
    public void RenderSanitizesControlCharactersAndTruncates()
    {
        var status = new TerminalProgressStatus(new TaskProgressStartedEventArgs(1, "Download\tpackage", TaskProgressUnit.Bytes), 0);
        status.Update(new TaskProgressUpdatedEventArgs(1, 1, 50, 100, "Receiving\r\ncontent"));

        string rendered = status.Render(25);

        rendered.ShouldNotContain("\t");
        rendered.ShouldNotContain("\r");
        rendered.ShouldNotContain("\n");
        rendered.Length.ShouldBe(25);
    }

    [Fact]
    public void OlderUpdatesDoNotReplaceCurrentState()
    {
        var status = new TerminalProgressStatus(new TaskProgressStartedEventArgs(1, "Download", TaskProgressUnit.Items), 0);
        status.Update(new TaskProgressUpdatedEventArgs(1, 2, 2, 10, "new"));
        status.Update(new TaskProgressUpdatedEventArgs(1, 1, 1, 10, "old"));

        status.Render(80).ShouldContain("[##-------- 2 of 10 items");
        status.Render(80).ShouldContain("new");
    }

    [Fact]
    public void RenderShowsBarForDeterminateProgress()
    {
        var status = new TerminalProgressStatus(new TaskProgressStartedEventArgs(1, "Download", TaskProgressUnit.Bytes), 0);
        status.Update(new TaskProgressUpdatedEventArgs(1, 1, 50, 100, "Receiving"));

        status.Render(80).ShouldContain("[#####----- 50 of 100 bytes");
    }

    [Fact]
    public void RenderNamesTheUnitWhenTheTotalIsUnknown()
    {
        var status = new TerminalProgressStatus(new TaskProgressStartedEventArgs(1, "Download", TaskProgressUnit.Bytes), 0);
        status.Update(new TaskProgressUpdatedEventArgs(1, 1, 1234, null, null));

        status.Render(80).ShouldContain("[1,234 bytes]");
    }

    [Fact]
    public void RenderOmitsTheUnitWhenTheOperationDidNotNameOne()
    {
        var status = new TerminalProgressStatus(new TaskProgressStartedEventArgs(1, "Work", TaskProgressUnit.Unspecified), 0);
        status.Update(new TaskProgressUpdatedEventArgs(1, 1, 3, 4, null));

        status.Render(80).ShouldContain("[#######--- 3 of 4]");
    }
}
