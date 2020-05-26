#nullable enable

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     <see cref="ProjectImportElement" /> implementation detail.
    /// </summary>
    internal readonly ref struct SdkReferenceSourceFullQuery
    {
        public readonly ProjectElement Element;
        public readonly SdkReferenceAttribute Sdk;
        public readonly SdkReferenceAttribute Version;
        public readonly SdkReferenceAttribute MinimumVersion;

        public SdkReferenceSourceFullQuery(ProjectElement element,
                                           SdkReferenceAttribute sdk,
                                           SdkReferenceAttribute version,
                                           SdkReferenceAttribute minimumVersion)
        {
            Sdk = sdk;
            Version = version;
            MinimumVersion = minimumVersion;
            Element = element;
        }
    }
}
