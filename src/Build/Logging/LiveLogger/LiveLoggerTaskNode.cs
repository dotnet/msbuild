// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.LiveLogger
{
    public class LiveLoggerTaskNode
    {
        public int Id;
        public string TaskName;
        public LiveLoggerTaskNode(TaskStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TaskId;
            TaskName = args.TaskName;
        }
    }
}
