namespace Microsoft.NET.Build.Containers;

internal sealed class RepositoryNotFoundException : Exception
{
    public RepositoryNotFoundException(string registry, string repositoryName, string reference)
        : base(string.Format("Unable to find the repository '{0}' at tag '{1}' in the registry '{2}'. Please confirm that this name and tag are present in the registry.", repositoryName, reference, registry))
    {
    }
}

