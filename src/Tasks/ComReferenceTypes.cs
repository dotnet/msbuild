// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Predefined typelib wrapper types.
    /// </summary>
    internal static class ComReferenceTypes
    {
        internal const string tlbimp = "tlbimp";
        internal const string aximp = "aximp";
        internal const string primary = "primary";
        internal const string primaryortlbimp = "primaryortlbimp";

        /// <summary>
        /// returns true is refType equals tlbimp
        /// </summary>
        internal static bool IsTlbImp(string refType)
        {
            return string.Equals(refType, ComReferenceTypes.tlbimp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// returns true is refType equals aximp
        /// </summary>
        internal static bool IsAxImp(string refType)
        {
            return string.Equals(refType, ComReferenceTypes.aximp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// returns true is refType equals pia
        /// </summary>
        internal static bool IsPia(string refType)
        {
            return string.Equals(refType, ComReferenceTypes.primary, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// returns true if refType equals primaryortlbimp, which is basically an unknown reference type
        /// </summary>
        internal static bool IsPiaOrTlbImp(string refType)
        {
            return string.Equals(refType, ComReferenceTypes.primaryortlbimp, StringComparison.OrdinalIgnoreCase);
        }
    }
}
