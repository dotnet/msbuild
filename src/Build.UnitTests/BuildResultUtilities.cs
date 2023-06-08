// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.Unittest
{
    internal sealed class BuildResultUtilities
    {
        public static TargetResult GetEmptyFailingTargetResult()
        {
            return new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetStopWithErrorResult());
        }

        public static TargetResult GetEmptySucceedingTargetResult()
        {
            return new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetSuccessResult());
        }

        public static TargetResult GetNonEmptySucceedingTargetResult()
        {
            return new TargetResult(new TaskItem[1] { new TaskItem("i", "v") }, BuildResultUtilities.GetSuccessResult());
        }

        public static WorkUnitResult GetSuccessResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null);
        }

        public static WorkUnitResult GetSkippedResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Skipped, WorkUnitActionCode.Continue, null);
        }

        public static WorkUnitResult GetStopWithErrorResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Stop, null);
        }

        public static WorkUnitResult GetStopWithErrorResult(Exception e)
        {
            return new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Stop, e);
        }

        public static WorkUnitResult GetContinueWithErrorResult()
        {
            return new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Continue, null);
        }
    }
}
