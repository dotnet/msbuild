namespace Microsoft.DotNet.Tools
{
    internal interface IFilePermissionSetter
    {
        void SetUserExecutionPermission(string path);
        void SetPermission(string path, string chmodArgument);
    }
}
