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

    public class FancyLoggerTaskNode
    {
        public int Id;
        public string TaskName;
        public FancyLoggerTaskNode(TaskStartedEventArgs args)
        {
            Id = args.BuildEventContext!.TaskId;
            TaskName = args.TaskName;
        }
    }
}
