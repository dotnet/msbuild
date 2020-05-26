#nullable enable

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     Interface for the XML-element-backed <see cref="ISdkReferenceSource" />.
    /// </summary>
    internal interface ISdkReferenceMutableSource : ISdkReferenceSource
    {
        SdkReferenceSourceFullQuery SdkReferenceFullQuery { get; }
        SdkReferenceSourceQuery SdkReferenceNameQuery { get; }
        SdkReferenceSourceQuery SdkReferenceVersionQuery { get; }
        SdkReferenceSourceQuery SdkReferenceMinimumVersionQuery { get; }
    }
}
