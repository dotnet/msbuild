namespace Microsoft.DotNet.Tools
{
    internal interface IFilePermissionSetter
    {
        void SetUserExecutionPermission(string path);
        void Set755Permission(string path);
    }
}
