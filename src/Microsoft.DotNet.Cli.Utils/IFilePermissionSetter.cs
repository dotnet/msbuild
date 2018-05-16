namespace Microsoft.DotNet.Cli.Utils
{
    internal interface IFilePermissionSetter
    {
        void SetUserExecutionPermission(string path);
    }
}
