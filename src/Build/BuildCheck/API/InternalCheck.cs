using System;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal abstract class InternalCheck : Check
{
    /// <summary>
    /// Used by the implementors to subscribe to data and events they are interested in.
    /// This offers superset of registrations options to <see cref="Check.RegisterActions"/>.
    /// </summary>
    /// <param name="registrationContext"></param>
    public abstract void RegisterInternalActions(IInternalCheckRegistrationContext registrationContext);

    /// <summary>
    /// This is intentionally not implemented, as it is extended by <see cref="RegisterInternalActions"/>.
    /// </summary>
    /// <param name="registrationContext"></param>
    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        if (registrationContext is not IInternalCheckRegistrationContext internalRegistrationContext)
        {
            throw new ArgumentException("The registration context for InternalBuildAnalyzer must be of type IInternalBuildCheckRegistrationContext.", nameof(registrationContext));
        }

        this.RegisterInternalActions(internalRegistrationContext);
    }
}
