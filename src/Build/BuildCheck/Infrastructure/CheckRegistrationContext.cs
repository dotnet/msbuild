// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Experimental.BuildCheck.Checks;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal sealed class CheckRegistrationContext(CheckWrapper checkWrapper, BuildCheckCentralContext buildCheckCentralContext)
    : IInternalCheckRegistrationContext
{
    public void RegisterEnvironmentVariableReadAction(Action<BuildCheckDataContext<EnvironmentVariableCheckData>> environmentVariableAction) =>
        buildCheckCentralContext.RegisterEnvironmentVariableReadAction(checkWrapper, environmentVariableAction);

    public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction) =>
        buildCheckCentralContext.RegisterEvaluatedPropertiesAction(checkWrapper, evaluatedPropertiesAction);

#pragma warning disable CS0618 // Type or member is obsolete
    public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction) =>
#pragma warning restore CS0618 // Type or member is obsolete
        buildCheckCentralContext.RegisterParsedItemsAction(checkWrapper, parsedItemsAction);

    public void RegisterEvaluatedItemsAction(Action<BuildCheckDataContext<EvaluatedItemsCheckData>> evaluatedItemsAction) =>
        buildCheckCentralContext.RegisterEvaluatedItemsAction(checkWrapper, evaluatedItemsAction);

    public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction) =>
        buildCheckCentralContext.RegisterTaskInvocationAction(checkWrapper, taskInvocationAction);

    public void RegisterPropertyReadAction(Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction)
        => buildCheckCentralContext.RegisterPropertyReadAction(checkWrapper, propertyReadAction);

    public void RegisterPropertyWriteAction(Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction)
        => buildCheckCentralContext.RegisterPropertyWriteAction(checkWrapper, propertyWriteAction);

    public void RegisterProjectRequestProcessingDoneAction(Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>> projectDoneAction)
        => buildCheckCentralContext.RegisterProjectRequestProcessingDoneAction(checkWrapper, projectDoneAction);

    public void RegisterBuildFinishedAction(Action<BuildCheckDataContext<BuildFinishedCheckData>> buildFinishedAction)
        => buildCheckCentralContext.RegisterBuildFinishedAction(checkWrapper, buildFinishedAction);

    public void RegisterProjectImportedAction(Action<BuildCheckDataContext<ProjectImportedCheckData>> projectImportedAction) =>
        buildCheckCentralContext.RegisterProjectImportedAction(checkWrapper, projectImportedAction);
}
