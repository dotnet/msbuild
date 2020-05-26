#nullable enable

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     <see cref="ProjectImportElement" /> implementation detail.
    /// </summary>
    internal readonly ref struct SdkReferenceSourceQuery
    {
        public readonly ProjectElement Element;
        public readonly SdkReferenceAttribute Factory;

        public SdkReferenceSourceQuery(ProjectElement element, SdkReferenceAttribute factory)
        {
            Element = element;
            Factory = factory;
        }
    }
}
