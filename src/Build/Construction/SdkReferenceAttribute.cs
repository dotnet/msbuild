#nullable enable

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     <see cref="ProjectImportElement" /> implementation detail.
    /// </summary>
    internal sealed class SdkReferenceAttribute
    {
        public readonly string AttributeName;
        public readonly string ChangeReasonMessage;

        public SdkReferenceAttribute(string attributeName, string changeReasonMessage)
        {
            AttributeName = attributeName;
            ChangeReasonMessage = changeReasonMessage;
        }
    }
}
