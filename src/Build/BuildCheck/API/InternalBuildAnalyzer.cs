using System;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Analyzers;

internal abstract class InternalBuildAnalyzer : BuildAnalyzer
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="registrationContext"></param>
    public abstract void RegisterInternalActions(IInternalBuildCheckRegistrationContext registrationContext);

    /// <summary>
    /// This is intentionally not implemented, as it is extended by <see cref="RegisterInternalActions"/>.
    /// </summary>
    /// <param name="registrationContext"></param>
    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        if (registrationContext is not IInternalBuildCheckRegistrationContext internalRegistrationContext)
        {
            throw new ArgumentException("The registration context for InternalBuildAnalyzer must be of type IInternalBuildCheckRegistrationContext.", nameof(registrationContext));
        }

        this.RegisterInternalActions(internalRegistrationContext);
    }
}
