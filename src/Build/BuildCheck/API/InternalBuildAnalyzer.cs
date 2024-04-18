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
    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext) { }
}