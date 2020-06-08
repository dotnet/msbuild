using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    internal interface IVsPublishMsBuildTaskHost
    {
        string TaskName { get; }

        TaskLoggingHelper Log { get; }

        IBuildEngine BuildEngine { get; }

        object GetProperty(string propertyName);
    }


    internal interface IVSMSDeployHost : IVsPublishMsBuildTaskHost
    {
        void PopulateOptions(/*DeploymentSyncOptions*/dynamic options);
        // Update the base config setting, hookup the event.
        void UpdateDeploymentBaseOptions(VSMSDeployObject srcVsMsDeployobject, VSMSDeployObject destVsMsDeployobject);
        // Unhook the event
        void ClearDeploymentBaseOptions(VSMSDeployObject srcVsMsDeployobject, VSMSDeployObject destVsMsDeployobject);
    }
}
