namespace Microsoft.DotNet.Cli.Utils
{
    public interface IEnvironmentPathInstruction
    {
        void PrintAddPathInstructionIfPathDoesNotExist();
    }
}