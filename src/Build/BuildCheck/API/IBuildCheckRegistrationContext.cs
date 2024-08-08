// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.BuildCheck;

public interface IBuildCheckRegistrationContext
{
    void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction);

    void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction);

    void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction);

    void RegisterBuildFinishedAction(Action<BuildCheckDataContext<BuildFinishedCheckData>> buildFinishedAction);
}
