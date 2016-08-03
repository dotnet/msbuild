namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Values for File Group Metadata
    /// </summary>
    internal enum FileGroup
    {
        CompileTimeAssembly,
        RuntimeAssembly,
        ContentFile,
        NativeLibrary,
        ResourceAssembly,
        RuntimeTarget,
        FrameworkAssembly
    }
}
