#nullable enable

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     Microsoft Build SDK reference values and their XML attributes' locations.
    /// </summary>
    internal readonly struct SdkReferenceWithOrigin
    {
        public readonly SdkReference Reference;
        public readonly SdkReferenceOrigin Origin;

        public SdkReferenceWithOrigin(SdkReference reference, SdkReferenceOrigin origin)
        {
            Reference = reference;
            Origin = origin;
        }
    }
}
