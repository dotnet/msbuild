// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections;
using System.Collections.Generic;

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
