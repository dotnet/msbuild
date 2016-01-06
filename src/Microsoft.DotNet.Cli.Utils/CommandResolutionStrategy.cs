namespace Microsoft.DotNet.Cli.Utils
{
    public enum CommandResolutionStrategy
    {
        //command loaded from a nuget package
        NugetPackage,

        //command loaded from the same directory as the executing assembly
        BaseDirectory,

        //command loaded from path
        Path,

        //command not found
        None
    }
}