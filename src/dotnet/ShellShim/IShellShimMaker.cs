namespace Microsoft.DotNet.ShellShim
{
    public interface IShellShimMaker
    {
        void CreateShim(string packageExecutablePath, string shellCommandName);
        void EnsureCommandNameUniqueness(string shellCommandName);
    }
}
