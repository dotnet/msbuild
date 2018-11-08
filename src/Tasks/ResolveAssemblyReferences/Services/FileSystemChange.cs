namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal struct FileSystemChange
    {
        internal string Directory { get; }

        internal string File { get; }

        internal FileSystemChange(string directory, string file)
        {
            Directory = directory;
            File = file;
        }
    }
}
