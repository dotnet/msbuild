#nullable enable

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     Represents a source of Microsoft Build SDK references data. Implementing this interface is not enough, you have to
    ///     also make changes to <see cref="ProjectImportElement" />.
    /// </summary>
    internal interface ISdkReferenceSource
    {
    }
}
