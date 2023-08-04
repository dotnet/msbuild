namespace Microsoft.NET.Build.Containers;

internal sealed class UnableToAccessRepositoryException : Exception
{
    public UnableToAccessRepositoryException(string registry, string repository) : base(string.Format("Unable to access the repository '{0}' in the registry '{1}'. Please confirm your credentials are correct and that you have access to this repository and registry.", repository, registry))
    {
    }
}
