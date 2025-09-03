// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal interface IInternalCheckRegistrationContext : IBuildCheckRegistrationContext
{
    void RegisterPropertyReadAction(Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction);

    void RegisterPropertyWriteAction(Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction);

    void RegisterProjectRequestProcessingDoneAction(Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>> propertyWriteAction);
}
