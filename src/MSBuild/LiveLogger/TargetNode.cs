// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    internal class TargetNode
    {
        public TargetNode(TargetStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TargetId;
            TargetName = string.IsNullOrEmpty(args?.TargetName)
                ? "task name"
                : args.TargetName;
        }

        public int Id { get; }

        public TaskNode? CurrentTaskNode { get; private set; }

        public string TargetName { get; }

        public TaskNode AddTask(TaskStartedEventArgs args)
        {
            CurrentTaskNode = new TaskNode(args);
            return CurrentTaskNode;
        }
    }
}
