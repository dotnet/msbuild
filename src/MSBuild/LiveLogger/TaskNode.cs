// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    internal class TaskNode
    {
        public TaskNode(TaskStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TaskId;
            TaskName = string.IsNullOrEmpty(args?.TaskName)
                ? "task name"
                : args.TaskName;
        }

        public int Id { get; }

        public string TaskName { get; }
    }
}
