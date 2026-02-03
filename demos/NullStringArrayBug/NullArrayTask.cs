// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NullStringArrayBug;

/// <summary>
/// A task that returns a string[] containing null elements.
/// This reproduces the bug in GitHub issue #13174.
/// </summary>
public class NullArrayTask : Task
{
    [Output]
    public string[]? OutputItems { get; set; }

    public override bool Execute()
    {
        // Pre-allocated array pattern - common when size is known but some slots remain unfilled
        OutputItems = new string[5];
        OutputItems[0] = "Item1";
        OutputItems[2] = "Item3";
        OutputItems[4] = "Item5";
        // Indices 1 and 3 are null

        Log.LogMessage(MessageImportance.High, "NullArrayTask executed - returning array with nulls at indices 1 and 3");
        return true;
    }
}
