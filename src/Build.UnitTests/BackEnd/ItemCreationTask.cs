﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

namespace ItemCreationTask
{
    public class ItemCreationTask : Task
    {
        public ITaskItem[] InputItemsToPassThrough
        {
            get;
            set;
        }

        public ITaskItem[] InputItemsToCopy
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] PassedThroughOutputItems
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] CreatedOutputItems
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] CopiedOutputItems
        {
            get;
            set;
        }

        [Output]
        public string OutputString
        {
            get;
            set;
        }

        public override bool Execute()
        {
            PassedThroughOutputItems = InputItemsToPassThrough;

            CopiedOutputItems = new ITaskItem[InputItemsToCopy.Length];

            for (int i = 0; i < InputItemsToCopy.Length; i++)
            {
                CopiedOutputItems[i] = new TaskItem(InputItemsToCopy[i]);
            }

            CreatedOutputItems = new ITaskItem[2];
            CreatedOutputItems[0] = new TaskItem("Foo");
            CreatedOutputItems[1] = new TaskItem("Bar");

            OutputString = "abc; def; ghi";

            return true;
        }
    }
}
