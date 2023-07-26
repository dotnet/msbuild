// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class GetPublishItemsOutputGroupOutputs : TaskBase
    {
        public ITaskItem[] ResolvedFileToPublish { get; set; }
        public string PublishDir { get; set; }

        [Output]
        public ITaskItem[] PublishItemsOutputGroupOutputs { get; private set; }

        protected override void ExecuteCore()
        {
            var list = new List<ITaskItem>();
            if (ResolvedFileToPublish != null)
            {
                foreach (var r in ResolvedFileToPublish)
                {
                    var newItem = new TaskItem(r.GetMetadata("FullPath"));
                    r.CopyMetadataTo(newItem);
                    newItem.SetMetadata(metadataName: MetadataKeys.TargetPath, metadataValue: r.GetMetadata(MetadataKeys.RelativePath));
                    newItem.SetMetadata(metadataName: "OutputPath", (PublishDir ?? "") + r.GetMetadata(MetadataKeys.RelativePath));
                    newItem.SetMetadata(metadataName: "OutputGroup", "PublishItemsOutputGroup");
                    list.Add(newItem);
                }
            }

            PublishItemsOutputGroupOutputs = list.ToArray();
        }
    }
}
