using System.Reflection;

namespace Microsoft.Extensions.Testing.Abstractions
{
    internal sealed class MissingPdbReader : IPdbReader
    {
        public static readonly MissingPdbReader Instance = new MissingPdbReader();

        public SourceInformation GetSourceInformation(MethodInfo methodInfo) => null;
        public void Dispose() { }
    }
}
