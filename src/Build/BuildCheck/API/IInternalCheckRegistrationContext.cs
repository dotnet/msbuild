using System;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal interface IInternalCheckRegistrationContext : IBuildCheckRegistrationContext
{
    void RegisterPropertyReadAction(Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction);

    void RegisterPropertyWriteAction(Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction);

    void RegisterProjectRequestProcessingDoneAction(Action<BuildCheckDataContext<ProjectRequestProcessingDoneData>> propertyWriteAction);
}
