// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.FancyLogger
{ 

    internal class FancyLoggerTargetNode
    {
        public int Id;
        public string TargetName;
        public FancyLoggerTaskNode? CurrentTaskNode;
        public FancyLoggerTargetNode(TargetStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TargetId;
            TargetName = args.TargetName;
        }
        public void AddTask(TaskStartedEventArgs args)
        {
            CurrentTaskNode = new FancyLoggerTaskNode(args);
        }
    }
}
