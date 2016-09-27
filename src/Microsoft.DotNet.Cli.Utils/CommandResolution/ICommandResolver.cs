namespace Microsoft.DotNet.Cli.Utils
{
    public interface ICommandResolver
    {
        CommandSpec Resolve(CommandResolverArguments arguments);
    }
}
