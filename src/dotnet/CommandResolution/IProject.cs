using System.Collections.Generic;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.CommandResolution
{
    internal interface IProject
    {
        LockFile GetLockFile();

        IEnumerable<SingleProjectInfo> GetTools();
    }
}