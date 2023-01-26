// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.LiveLogger
{ 

    public class LiveLoggerTargetNode
    {
        public int Id;
        public string TargetName;
        public LiveLoggerTaskNode? CurrentTaskNode;
        public LiveLoggerTargetNode(TargetStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TargetId;
            TargetName = args.TargetName;
        }
        public LiveLoggerTaskNode AddTask(TaskStartedEventArgs args)
        {
            CurrentTaskNode = new LiveLoggerTaskNode(args);
            return CurrentTaskNode;
        }
    }
}
