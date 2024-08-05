// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal sealed class BuildCheckRegistrationContext(BuildExecutionCheckWrapper checkWrapper, BuildCheckCentralContext buildCheckCentralContext) : IBuildCheckRegistrationContext
{
    public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction)
    {
        buildCheckCentralContext.RegisterEvaluatedPropertiesAction(checkWrapper, evaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction)
    {
        buildCheckCentralContext.RegisterParsedItemsAction(checkWrapper, parsedItemsAction);
    }

    public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction)
    {
        buildCheckCentralContext.RegisterTaskInvocationAction(checkWrapper, taskInvocationAction);
    }
}
