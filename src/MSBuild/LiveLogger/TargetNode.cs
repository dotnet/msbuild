// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.LiveLogger
{

    internal class TargetNode
    {
        public int Id;
        public string TargetName;
        public TaskNode? CurrentTaskNode;
        public TargetNode(TargetStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TargetId;
            TargetName = args.TargetName;
        }
        public TaskNode AddTask(TaskStartedEventArgs args)
        {
            CurrentTaskNode = new TaskNode(args);
            return CurrentTaskNode;
        }
    }
}
