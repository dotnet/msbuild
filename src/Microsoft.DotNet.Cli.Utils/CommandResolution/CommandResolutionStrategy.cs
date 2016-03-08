namespace Microsoft.DotNet.Cli.Utils
{
    public enum CommandResolutionStrategy
    {
        // command loaded from project dependencies nuget package
        ProjectDependenciesPackage,

        // command loaded from project tools nuget package
        ProjectToolsPackage,

        // command loaded from the same directory as the executing assembly
        BaseDirectory,

        // command loaded from the same directory as a project.json file
        ProjectLocal,

        // command loaded from PATH environment variable
        Path,

        // command loaded from rooted path
        RootedPath,

        // command not found
        None
    }
}