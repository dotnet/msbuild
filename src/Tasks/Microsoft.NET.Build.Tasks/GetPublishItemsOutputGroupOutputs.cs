// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
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
