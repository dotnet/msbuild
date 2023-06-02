// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace NullMetadataTask
{
    public class NullMetadataTask : Task
    {
        [Output]
        public ITaskItem[] OutputItems
        {
            get;
            set;
        }

        public override bool Execute()
        {
            OutputItems = new ITaskItem[1];

            IDictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("a", null);

            OutputItems[0] = new TaskItem("foo", (IDictionary)metadata);

            return true;
        }
    }
}
