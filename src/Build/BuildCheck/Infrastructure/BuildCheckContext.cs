// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal sealed class BuildCheckRegistrationContext(CheckWrapper checkWrapper, BuildCheckCentralContext buildCheckCentralContext) : IBuildCheckRegistrationContext
{
    public void RegisterEnvironmentVariableReadAction(Action<BuildCheckDataContext<EnvironmentVariableCheckData>> environmentVariableAction) =>
        buildCheckCentralContext.RegisterEnvironmentVariableReadAction(checkWrapper, environmentVariableAction);

    public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction) =>
        buildCheckCentralContext.RegisterEvaluatedPropertiesAction(checkWrapper, evaluatedPropertiesAction);

    public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction) =>
        buildCheckCentralContext.RegisterParsedItemsAction(checkWrapper, parsedItemsAction);

    public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction) =>
        buildCheckCentralContext.RegisterTaskInvocationAction(checkWrapper, taskInvocationAction);

    public void RegisterBuildFinishedAction(Action<BuildCheckDataContext<BuildFinishedCheckData>> buildFinishedAction) => 
        buildCheckCentralContext.RegisterBuildFinishedAction(checkWrapper, buildFinishedAction);
}
