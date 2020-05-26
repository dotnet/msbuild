#nullable enable

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     <see cref="ProjectImportElement" /> implementation detail for the &lt;Project /&gt; "Sdk" attribute.
    /// </summary>
    internal sealed class SdkReferenceConstantSource : ISdkReferenceSource
    {
        private readonly SdkReferenceWithOrigin _sdkReference;

        public SdkReferenceConstantSource(in SdkReferenceWithOrigin source) => _sdkReference = source;

        public ref readonly SdkReferenceWithOrigin SdkReference => ref _sdkReference;
    }
}
