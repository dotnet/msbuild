// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal sealed class BuildCheckRegistrationContext(CheckWrapper checkWrapper, BuildCheckCentralContext buildCheckCentralContext) : IInternalBuildCheckRegistrationContext
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

    public void RegisterPropertyReadAction(Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction)
        => buildCheckCentralContext.RegisterPropertyReadAction(checkWrapper, propertyReadAction);

    public void RegisterPropertyWriteAction(Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction)
        => buildCheckCentralContext.RegisterPropertyWriteAction(checkWrapper, propertyWriteAction);

    public void RegisterProjectRequestProcessingDoneAction(Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>> projectDoneAction)
        => buildCheckCentralContext.RegisterProjectRequestProcessingDoneAction(checkWrapper, projectDoneAction);

    public void RegisterBuildFinishedAction(Action<BuildCheckDataContext<BuildFinishedCheckData>> buildFinishedAction)
        => buildCheckCentralContext.RegisterBuildFinishedAction(checkWrapper, buildFinishedAction);
}
