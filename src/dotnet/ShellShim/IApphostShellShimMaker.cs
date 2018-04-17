using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal interface IAppHostShellShimMaker
    {
        void CreateApphostShellShim(FilePath entryPoint, FilePath shimPath);
    }
}
