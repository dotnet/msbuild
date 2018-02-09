using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    public interface IShellShimMaker
    {
        void CreateShim(FilePath packageExecutable, string shellCommandName);
        void EnsureCommandNameUniqueness(string shellCommandName);
    }
}
