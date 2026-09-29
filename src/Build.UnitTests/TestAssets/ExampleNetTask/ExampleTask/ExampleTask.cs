// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;

namespace NetTask
{
    public class ExampleTask : Microsoft.Build.Utilities.Task
    {
        public enum CopyMode
        {
            Shallow,
            Deep,
        }

        // nullable isn't available in net framework runtime
        // the presence of the property covers the test case
        public string? OutputValue { get; set; }

        public bool Flag { get; set; }

        public string? Text { get; set; }

        public ITaskItem? Item { get; set; }

        public CopyMode Mode { get; set; }

        public CopyMode[]? Modes { get; set; }

        [Output]
        public FileInfo? DestinationFile { get; set; }

        [Output]
        public FileInfo[]? DestinationFiles { get; set; }

        [Output]
        public DirectoryInfo? DestinationDirectory { get; set; }

#if NET
        [Output]
        public DateOnly OutputDate { get; set; } = new(2026, 9, 4);

        [Output]
        public DateOnly[] OutputDates { get; set; } = [new(2026, 9, 4), new(2026, 9, 5)];

        [Output]
        public GenericTaskItem<DateOnly>? TypedItem { get; set; }

        [Output]
        public GenericTaskItem<DateOnly>[]? TypedItems { get; set; }

        [Output]
        public GenericTaskItem<DateOnly>? NullTypedItem { get; set; }
#endif

        public override bool Execute()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var executingProcess = currentProcess.ProcessName;
                var processPath = currentProcess.MainModule?.FileName ?? "Unknown";

                Log.LogMessage(MessageImportance.High, $"The task is executed in process: {executingProcess} with id {currentProcess.Id}");
                Log.LogMessage(MessageImportance.High, $"Process path: {processPath}");

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    Log.LogMessage(MessageImportance.High, $"Arg[{i}]: {args[i]}");
                }

                Log.LogMessage(
                    MessageImportance.High,
                    $"PARAMETER_BINDING_OK Flag={Flag} Text={Text} Item={Item?.ItemSpec}:{Item?.GetMetadata("Kind")} Mode={Mode} Modes={string.Join(",", Modes ?? [])} File={DestinationFile?.FullName} Files={string.Join(",", Array.ConvertAll(DestinationFiles ?? [], file => file.FullName))} Directory={DestinationDirectory?.FullName}");

#if NET
                TypedItem = new("typed.item");
                TypedItem.SetMetadata("Kind", "scalar");
                TypedItems = [new("first.item"), new("second.item")];
                TypedItems[0].SetMetadata("Kind", "first");
                TypedItems[1].SetMetadata("Kind", "second");
#endif

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to determine executing process: {ex.Message}");
                return false;
            }
        }
    }

    public sealed class GenericTaskItem<T>(string itemSpec) : ITaskItem
    {
        private readonly Microsoft.Build.Utilities.TaskItem _item = new(itemSpec);

        public string ItemSpec
        {
            get => _item.ItemSpec;
            set => _item.ItemSpec = value;
        }

        public ICollection MetadataNames => _item.MetadataNames;

        public int MetadataCount => _item.MetadataCount;

        public string GetMetadata(string metadataName) => _item.GetMetadata(metadataName);

        public void SetMetadata(string metadataName, string metadataValue) => _item.SetMetadata(metadataName, metadataValue);

        public void RemoveMetadata(string metadataName) => _item.RemoveMetadata(metadataName);

        public void CopyMetadataTo(ITaskItem destinationItem) => _item.CopyMetadataTo(destinationItem);

        public IDictionary CloneCustomMetadata() => _item.CloneCustomMetadata();
    }
}
