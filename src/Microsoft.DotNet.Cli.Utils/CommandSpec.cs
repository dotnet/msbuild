namespace Microsoft.DotNet.Cli.Utils
{
    internal class CommandSpec
    {
        public CommandSpec(string path, string args, CommandResolutionStrategy resolutionStrategy)
        {
            Path = path;
            Args = args;
            ResolutionStrategy = resolutionStrategy;
        }

        public string Path { get; }

        public string Args { get; }

        public CommandResolutionStrategy ResolutionStrategy { get; }
    }
}