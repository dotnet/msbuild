using System;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal interface IInternalBuildCheckRegistrationContext : IBuildCheckRegistrationContext
{
    void RegisterPropertyReadAction(Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction);

    void RegisterPropertyWriteAction(Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction);

    void RegisterProjectRequestProcessingDoneAction(Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>> propertyWriteAction);
}
