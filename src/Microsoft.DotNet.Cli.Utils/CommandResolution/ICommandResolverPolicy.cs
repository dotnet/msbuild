namespace Microsoft.DotNet.Cli.Utils
{
    public interface ICommandResolverPolicy
    {
        CompositeCommandResolver CreateCommandResolver();
    }
}