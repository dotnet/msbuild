namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public interface IDiagnostic
    {
        string DiagnosticId { get; }
        string ReferenceId { get; }
    }
}
