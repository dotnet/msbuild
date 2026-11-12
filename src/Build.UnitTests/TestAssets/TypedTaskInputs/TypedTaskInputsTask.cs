// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using Microsoft.Build.Framework;

public sealed class TypedTaskInputsTask : Microsoft.Build.Utilities.Task
{
    public FileInfo File { get; set; } = null!;
    public FileInfo[] Files { get; set; } = [];
    public DirectoryInfo Directory { get; set; } = null!;
    public DirectoryInfo[] Directories { get; set; } = [];
    public AbsolutePath Path { get; set; }
    public AbsolutePath[] Paths { get; set; } = [];
    public TaskItem<int> NumberItem { get; set; }
    public ITaskItem<int> NumberInterface { get; set; } = null!;
    public TaskItem<int>[] NumberItems { get; set; } = [];
    public ITaskItem<int>[] NumberInterfaces { get; set; } = [];
    public string ExpectedFile { get; set; } = "";
    public string ExpectedDirectory { get; set; } = "";

    [Output]
    public bool Verified { get; private set; }

    public override bool Execute()
    {
        Verified = File.FullName == ExpectedFile
            && Directory.FullName == ExpectedDirectory
            && Path.Value == ExpectedFile
            && NumberItem.Value == 41
            && NumberItem.GetMetadata("Tag") == "scalar"
            && NumberInterface.Value == 41
            && NumberInterface.GetMetadata("Tag") == "scalar"
            && Files.Length == 2
            && Directories.Length == 2
            && Paths.Length == 2
            && NumberItems.Length == 2
            && NumberInterfaces.Length == 2;

        for (int i = 0; Verified && i < 2; i++)
        {
            string expectedTag = i == 0 ? "first" : "second";
            Verified = Files[i].FullName == ExpectedFile
                && Directories[i].FullName == ExpectedDirectory
                && Paths[i].Value == ExpectedFile
                && NumberItems[i].Value == 41 + i
                && NumberItems[i].GetMetadata("Tag") == expectedTag
                && NumberInterfaces[i].Value == 41 + i
                && NumberInterfaces[i].GetMetadata("Tag") == expectedTag;
        }

        if (!Verified)
        {
            Log.LogError("Typed task input values, rooted paths, or metadata did not match.");
        }

        return Verified;
    }
}
